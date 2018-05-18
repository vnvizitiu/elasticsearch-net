﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bogus;
using Tests.Framework.Versions;

namespace Tests.Framework.Configuration
{
	public class YamlConfiguration : TestConfigurationBase
	{
		private readonly Dictionary<string, string> _config;
		public sealed override bool TestAgainstAlreadyRunningElasticsearch { get; protected set; } = true;
		public sealed override ElasticsearchVersion ElasticsearchVersion { get; protected set; }
		public sealed override bool ForceReseed { get; protected set; } = true;
		public sealed override TestMode Mode { get; protected set; } = TestMode.Unit;
		public sealed override string ClusterFilter { get; protected set; }
		public sealed override string TestFilter { get; protected set; }
		public sealed override int Seed { get; protected set; }

		public YamlConfiguration(string configurationFile)
		{
			if (!File.Exists(configurationFile)) return;

			_config = File.ReadAllLines(configurationFile)
				.Where(l => !l.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(l))
				.ToDictionary(ConfigName, ConfigValue);

			this.Mode = GetTestMode(_config["mode"]);
			this.ElasticsearchVersion = ElasticsearchVersion.Create(_config["elasticsearch_version"]);
			this.ForceReseed = BoolConfig("force_reseed", false);
			this.TestAgainstAlreadyRunningElasticsearch = BoolConfig("test_against_already_running_elasticsearch", false);
			this.ClusterFilter = _config.ContainsKey("cluster_filter") ? _config["cluster_filter"] : null;
			this.TestFilter = _config.ContainsKey("test_filter") ? _config["test_filter"] : null;

			this.Seed = _config.TryGetValue("seed", out var seed) ? int.Parse(seed) : 1337;
		    Randomizer.Seed = new Random(this.Seed);
			var randomizer = new Randomizer();
			this.Random = new RandomConfiguration
			{
				SourceSerializer = RandomBool("source_serializer", randomizer),
				TypedKeys = RandomBool("typed_keys", randomizer),
				OldConnection = RandomBool("old_connection", randomizer)
			};
		}

		private bool BoolConfig(string key, bool @default) =>
			_config.TryGetValue(key, out var v) ? bool.Parse(v) : @default;

		private bool RandomBool(string key, Randomizer randomizer) =>
			_config.TryGetValue($"random_{key}", out var v) ? bool.Parse(v) : randomizer.Bool();

		private static string ConfigName(string configLine) => Parse(configLine, 0);
		private static string ConfigValue(string configLine) => Parse(configLine, 1);
		private static string Parse(string configLine, int index) => configLine.Split(':')[index].Trim(' ');

		private static TestMode GetTestMode(string mode)
		{
			switch (mode)
			{
				case "unit":
				case "u":
					return TestMode.Unit;
				case "integration":
				case "i":
					return TestMode.Integration;
				case "mixed":
				case "m":
					return TestMode.Mixed;
				default:
					throw new ArgumentException($"Unknown test mode: {mode}");
			}
		}
	}
}
