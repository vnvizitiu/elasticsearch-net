﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Elasticsearch.Net;
using Nest;
using Tests.Framework.Configuration;
using Tests.Framework.ManagedElasticsearch.NodeSeeders;
using Tests.Framework.ManagedElasticsearch.SourceSerializers;
using Tests.Framework.MockData;
using Tests.Framework.Versions;

#if FEATURE_HTTPWEBREQUEST
using Elasticsearch.Net.Connections.HttpWebRequestConnection;
#endif


namespace Tests.Framework
{
	public static class TestClient
	{
		public static readonly bool RunningFiddler = Process.GetProcessesByName("fiddler").Any();
		public static readonly ITestConfiguration Configuration = LoadConfiguration();

		public static readonly ConnectionSettings GlobalDefaultSettings = CreateSettings();
		public static readonly IElasticClient Default = new ElasticClient(GlobalDefaultSettings);
		public static readonly IElasticClient DefaultInMemoryClient = GetInMemoryClient();

		public static ConcurrentBag<string> SeenDeprecations { get; } = new ConcurrentBag<string>();

		public static Uri CreateUri(int port = 9200, bool forceSsl = false) =>
			new UriBuilder(forceSsl ? "https" : "http", Host, port).Uri;

		public static string DefaultHost => "localhost";
		private static string Host => (RunningFiddler) ? "ipv4.fiddler" : DefaultHost;

		private static ITestConfiguration LoadConfiguration()
		{
			// The build script sets a FAKEBUILD env variable, so if it exists then
			// we must be running tests from the build script
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FAKEBUILD")))
				return new EnvironmentConfiguration();

			var directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());

			// If running the classic .NET solution, tests run from bin/{config} directory,
			// but when running DNX solution, tests run from the test project root
			var yamlConfigurationPath = (directoryInfo.Name == "Tests"
			                             && directoryInfo.Parent != null
			                             && directoryInfo.Parent.Name == "src")
				? "."
				: @"../../../";

			var localYamlFile = Path.GetFullPath(Path.Combine(yamlConfigurationPath, "tests.yaml"));
			if (File.Exists(localYamlFile))
				return new YamlConfiguration(localYamlFile);

			var defaultYamlFile = Path.GetFullPath(Path.Combine(yamlConfigurationPath, "tests.default.yaml"));
			if (File.Exists(defaultYamlFile))
				return new YamlConfiguration(defaultYamlFile);

			throw new Exception($"Tried to load a yaml file from {yamlConfigurationPath} but it does not exist : pwd:{directoryInfo.FullName}");
		}

		private static int ConnectionLimitDefault =>
			int.TryParse(Environment.GetEnvironmentVariable("NEST_NUMBER_OF_CONNECTIONS"), out int x)
				? x
				: ConnectionConfiguration.DefaultConnectionLimit;

		private static ConnectionSettings DefaultSettings(ConnectionSettings settings) => settings
			.DefaultIndex("default-index")
			.DefaultMappingFor<Project>(map => map
				.IndexName(DefaultSeeder.ProjectsIndex)
				.IdProperty(p => p.Name)
				.RelationName("project")
				.TypeName("doc")
			)
			.DefaultMappingFor<CommitActivity>(map => map
				.IndexName(DefaultSeeder.ProjectsIndex)
				.RelationName("commits")
				.TypeName("doc")
			)
			.DefaultMappingFor<Developer>(map => map
				.IndexName("devs")
				.Ignore(p => p.PrivateValue)
				.PropertyName(p => p.OnlineHandle, "nickname")
			)
			.DefaultMappingFor<ProjectPercolation>(map => map
				.IndexName("queries")
				.TypeName(PercolatorType)
			)
			.DefaultMappingFor<Metric>(map => map
				.IndexName("server-metrics")
				.TypeName("metric")
			)
			.DefaultMappingFor<Shape>(map => map
				.IndexName("shapes")
				.TypeName("doc")
			)
			.ConnectionLimit(ConnectionLimitDefault)
			//TODO make this random
			//.EnableHttpCompression()
			.OnRequestCompleted(r =>
			{
				if (!r.DeprecationWarnings.Any()) return;
				var q = r.Uri.Query;
				//hack to prevent the deprecation warnings from the deprecation response test to be reported
				if (!string.IsNullOrWhiteSpace(q) && q.Contains("routing=ignoredefaultcompletedhandler")) return;
				foreach (var d in r.DeprecationWarnings) SeenDeprecations.Add(d);
			});

		public static string PercolatorType => Configuration.ElasticsearchVersion <= ElasticsearchVersion.Create("5.0.0-alpha1")
			? ".percolator"
			: "query";

		private static bool VersionSatisfiedBy(string range, ElasticsearchVersion version)
		{
			var versionRange = new SemVer.Range(range);
			var satisfied = versionRange.IsSatisfied(version.Version);
			if (version.State != ElasticsearchVersion.ReleaseState.Released || satisfied)
				return satisfied;

			//Semver can only match snapshot version with ranges on the same major and minor
			//anything else fails but we want to know e.g 2.4.5-SNAPSHOT satisfied by <5.0.0;
			var wholeVersion = $"{version.Major}.{version.Minor}.{version.Patch}";
			return versionRange.IsSatisfied(wholeVersion);
		}

		public static bool VersionUnderTestSatisfiedBy(string range) =>
			VersionSatisfiedBy(range, TestClient.Configuration.ElasticsearchVersion);

		public static ConnectionSettings CreateSettings(
			Func<ConnectionSettings, ConnectionSettings> modifySettings = null,
			int port = 9200,
			bool forceInMemory = false,
			bool forceSsl = false,
			Func<Uri, IConnectionPool> createPool = null,
			ConnectionSettings.SourceSerializerFactory sourceSerializerFactory = null,
			IPropertyMappingProvider propertyMappingProvider = null
		)
		{
			createPool = createPool ?? (u => new SingleNodeConnectionPool(u));

			var connectionPool = createPool(CreateUri(port, forceSsl));
			var connection = CreateConnection(forceInMemory: forceInMemory);
			var s = new ConnectionSettings(connectionPool, connection, (builtin, values) =>
			{
				if (sourceSerializerFactory != null) return sourceSerializerFactory(builtin, values);

				return !Configuration.Random.SourceSerializer
					? null
					: new TestSourceSerializerBase(builtin, values);
			}, propertyMappingProvider);

			var defaultSettings = DefaultSettings(s);

			modifySettings = modifySettings ?? ((m) =>
			{
				//only enable debug mode when running in DEBUG mode (always) or optionally wheter we are executing unit tests
				//during RELEASE builds tests
#if !DEBUG
			if (TestClient.Configuration.RunUnitTests)
#endif
				m.EnableDebugMode();
				return m;
			});

			var settings = modifySettings(defaultSettings);
			return settings;
		}

		public static IElasticClient GetInMemoryClient() => new ElasticClient(GlobalDefaultSettings);

		public static IElasticClient GetInMemoryClient(Func<ConnectionSettings, ConnectionSettings> modifySettings, int port = 9200) =>
			new ElasticClient(CreateSettings(modifySettings, port, forceInMemory: true).EnableDebugMode());

		public static IElasticClient GetInMemoryClientWithSourceSerializer(
			Func<ConnectionSettings, ConnectionSettings> modifySettings,
			ConnectionSettings.SourceSerializerFactory sourceSerializerFactory = null,
			IPropertyMappingProvider propertyMappingProvider = null) =>
			new ElasticClient(
				CreateSettings(modifySettings, forceInMemory: true, sourceSerializerFactory: sourceSerializerFactory,
					propertyMappingProvider: propertyMappingProvider)
			);

		public static IElasticClient GetClient(
			Func<ConnectionSettings, ConnectionSettings> modifySettings = null,
			int port = 9200,
			bool forceSsl = false,
			ConnectionSettings.SourceSerializerFactory sourceSerializerFactory = null) =>
			new ElasticClient(
				CreateSettings(modifySettings, port, forceInMemory: false, forceSsl: forceSsl, sourceSerializerFactory: sourceSerializerFactory)
			);

		public static IElasticClient GetClient(
			Func<Uri, IConnectionPool> createPool,
			Func<ConnectionSettings, ConnectionSettings> modifySettings = null,
			int port = 9200) =>
			new ElasticClient(CreateSettings(modifySettings, port, forceInMemory: false, createPool: createPool));

		public static IConnection CreateConnection(ConnectionSettings settings = null, bool forceInMemory = false) =>
			Configuration.RunIntegrationTests && !forceInMemory ? CreateLiveConnection() : new InMemoryConnection();

		private static IConnection CreateLiveConnection()
		{
#if FEATURE_HTTPWEBREQUEST
			if (Configuration.Random.OldConnection) return new HttpWebRequestConnection();
			return new HttpConnection();
#else
			return new HttpConnection();
#endif
		}

		public static IElasticClient GetFixedReturnClient(
			object response,
			int statusCode = 200,
			Func<ConnectionSettings, ConnectionSettings> modifySettings = null,
			string contentType = RequestData.MimeType,
			Exception exception = null)
		{
			var settings = GetFixedReturnSettings(response, statusCode, modifySettings, contentType, exception);
			return new ElasticClient(settings);
		}

		public static ConnectionSettings GetFixedReturnSettings(
			object response,
			int statusCode = 200,
			Func<ConnectionSettings, ConnectionSettings> modifySettings = null,
			string contentType = RequestData.MimeType,
			Exception exception = null)
		{
			var serializer = Default.RequestResponseSerializer;
			byte[] fixedResult = null;
			if (response is string s) fixedResult = Encoding.UTF8.GetBytes(s);
			else if (contentType == RequestData.MimeType)
				fixedResult = serializer.SerializeToBytes(response);
			else
				fixedResult = Encoding.UTF8.GetBytes(response.ToString());

			var connection = new InMemoryConnection(fixedResult, statusCode, exception, contentType);
			var connectionPool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var defaultSettings = new ConnectionSettings(connectionPool, connection)
				.DefaultIndex("default-index");
			var settings = (modifySettings != null) ? modifySettings(defaultSettings) : defaultSettings;
			return settings;
		}
	}
}
