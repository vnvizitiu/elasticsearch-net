﻿using System;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.MockData;

namespace Tests.Aggregations.Metric.ScriptedMetric
{
	public class ScriptedMetricAggregationUsageTests : AggregationUsageTestBase
	{
		public ScriptedMetricAggregationUsageTests(ReadOnlyCluster i, EndpointUsage usage) : base(i, usage) { }

		protected override object ExpectJson => new
		{
			aggs = new
			{
				sum_the_hard_way = new
				{
					scripted_metric = new
					{
						init_script = new
						{
							inline = "_agg['commits'] = []",
							lang = "groovy"
						},
						map_script = new
						{
							inline = "if (doc['state'].value == \"Stable\") { _agg.commits.add(doc['numberOfCommits']) }",
							lang = "groovy"
						},
						combine_script = new
						{
							inline = "sum = 0; for (c in _agg.commits) { sum += c }; return sum",
							lang = "groovy"
						},
						reduce_script = new
						{
							inline = "sum = 0; for (a in _aggs) { sum += a }; return sum",
							lang = "groovy"
						}
					}
				}
			}
		};

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Aggregations(a => a
				.ScriptedMetric("sum_the_hard_way", sm => sm
					.InitScript(ss=>ss.Inline("_agg['commits'] = []").Lang("groovy"))
					.MapScript(ss=>ss.Inline("if (doc['state'].value == \"Stable\") { _agg.commits.add(doc['numberOfCommits']) }").Lang("groovy"))
					.CombineScript(ss=>ss.Inline("sum = 0; for (c in _agg.commits) { sum += c }; return sum").Lang("groovy"))
					.ReduceScript(ss=>ss.Inline("sum = 0; for (a in _aggs) { sum += a }; return sum").Lang("groovy"))
				)
			);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Aggregations = new ScriptedMetricAggregation("sum_the_hard_way")
				{
					InitScript = new InlineScript("_agg['commits'] = []") { Lang = "groovy" },
					MapScript = new InlineScript("if (doc['state'].value == \"Stable\") { _agg.commits.add(doc['numberOfCommits']) }"){ Lang = "groovy" },
					CombineScript = new InlineScript("sum = 0; for (c in _agg.commits) { sum += c }; return sum"){ Lang = "groovy" },
					ReduceScript = new InlineScript("sum = 0; for (a in _aggs) { sum += a }; return sum"){ Lang = "groovy" }
				}
			};

		protected override void ExpectResponse(ISearchResponse<Project> response)
		{
			response.ShouldBeValid();
			var sumTheHardWay = response.Aggs.ScriptedMetric("sum_the_hard_way");
			sumTheHardWay.Should().NotBeNull();
			sumTheHardWay.Value<int>().Should().BeGreaterThan(0);
		}
	}
}
