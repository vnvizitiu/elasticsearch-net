﻿using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.MockData;
using Xunit;

namespace Tests.Document.Multiple.Reindex
{
	public class ReindexCluster : ClusterBase
	{
		public override void Bootstrap()
		{
			var seeder = new Seeder(this.Node);
			seeder.DeleteIndicesAndTemplates();
			seeder.CreateIndices();
		}
	}

	public class ReindexApiTests : SerializationTestBase, IClusterFixture<ReindexCluster>
	{
		private readonly IObservable<IReindexResponse<ILazyDocument>> _reindexManyTypesResult;
		private readonly IObservable<IReindexResponse<Project>> _reindexSingleTypeResult;
		private readonly IElasticClient _client;

		private static string NewManyTypesIndexName { get; } = $"project-copy-{Guid.NewGuid().ToString("N").Substring(8)}";

		private static string NewSingleTypeIndexName { get; } = $"project-copy-{Guid.NewGuid().ToString("N").Substring(8)}";

		private static string IndexName { get; } = "project";

		public ReindexApiTests(ReindexCluster cluster, EndpointUsage usage)
		{
			this._client = cluster.Client;

			// create a couple of projects
			var projects = Project.Generator.Generate(2).ToList();
			var indexProjectsResponse = this._client.IndexMany(projects, IndexName);
			this._client.Refresh(IndexName);

			// create a thousand commits and associate with the projects
			var commits = CommitActivity.CommitActivities;
			var bb = new BulkDescriptor();
			for (int i = 0; i < commits.Count; i++)
			{
				var commit = commits[i];
				var project = i % 2 == 0
					? projects[0].Name
					: projects[1].Name;

				bb.Index<CommitActivity>(bi => bi
					.Document(commit)
					.Index(IndexName)
					.Id(commit.Id)
					.Routing(project)
					.Parent(project)
				);
			}

			var bulkResult = this._client.Bulk(b => bb);
			bulkResult.ShouldBeValid();

			this._client.Refresh(IndexName);

			this._reindexManyTypesResult = this._client.Reindex<ILazyDocument>(IndexName, NewManyTypesIndexName, r => r.AllTypes());
			this._reindexSingleTypeResult = this._client.Reindex<Project>(IndexName, NewSingleTypeIndexName);
		}

		[I]
		public void ReturnsExpectedResponse()
		{
			var handles = new[]
			{
				new ManualResetEvent(false),
				new ManualResetEvent(false)
			};

			var manyTypesObserver = new ReindexObserver<ILazyDocument>(
				onError: (e) => { throw e; },
				onCompleted: () => ReindexManyTypesCompleted(handles),
				alter: (h, d, i) =>
				{
					//This would be a great place to alter the
					// BulkIndexOperation i (use pipeline perhaps)
					// Source Document d
					// h is the whole hit including the documents metadata
					//
					// on a per document basis
					i.RetriesOnConflict = 2;
				}
			);

			this._reindexManyTypesResult.Subscribe(manyTypesObserver);

			var singleTypeObserver = new ReindexObserver<Project>(
				onError: (e) => { throw e; },
				onCompleted: () => ReindexSingleTypeCompleted(handles)
			);
			this._reindexSingleTypeResult.Subscribe(singleTypeObserver);


			WaitHandle.WaitAll(handles, TimeSpan.FromMinutes(3));
		}

		private void ReindexSingleTypeCompleted(ManualResetEvent[] handles)
		{
			var refresh = this._client.Refresh(NewSingleTypeIndexName);
			var originalIndexCount = this._client.Count<Project>(c => c.Index(IndexName));

			// new index should only contain project document types
			var newIndexCount = this._client.Count<Project>(c => c.Index(NewSingleTypeIndexName).AllTypes());

			originalIndexCount.Count.Should().BeGreaterThan(0).And.Be(newIndexCount.Count);

			handles[1].Set();
		}

		private void ReindexManyTypesCompleted(ManualResetEvent[] handles)
		{
			var refresh = this._client.Refresh(NewManyTypesIndexName);
			var originalIndexCount = this._client.Count<CommitActivity>(c => c.Index(IndexName));
			var newIndexCount = this._client.Count<CommitActivity>(c => c.Index(NewManyTypesIndexName));

			originalIndexCount.Count.Should().BeGreaterThan(0).And.Be(newIndexCount.Count);

			var scroll = "20s";

			var searchResult = this._client.Search<CommitActivity>(s => s
				.Index(NewManyTypesIndexName)
				.From(0)
				.Size(100)
				.Query(q => q.MatchAll())
				.Scroll(scroll)
			);

			do
			{
				var result = searchResult;
				searchResult = this._client.Scroll<CommitActivity>(scroll, result.ScrollId);
				foreach (var hit in searchResult.Hits)
				{
					hit.Parent.Should().NotBeNullOrEmpty();
					hit.Routing.Should().NotBeNullOrEmpty();
				}
			} while (searchResult.IsValid && searchResult.Documents.Any());
			handles[0].Set();
		}
	}
}
