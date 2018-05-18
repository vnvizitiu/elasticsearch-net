﻿using System;
using System.Reflection;
using Bogus;
using Tests.Framework;
using Tests.Framework.Configuration;
using Tests.Framework.MockData;
using Xunit.Abstractions;
using Xunit.Sdk;
using static System.Console;

namespace Xunit
{
	internal class NestTestFramework : XunitTestFramework
	{
		public NestTestFramework(IMessageSink messageSink)
			: base(messageSink)
		{
			Generators.Initialize();
		}

		protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
		{
			var config = TestClient.Configuration;
		    Randomizer.Seed = new Random(config.Seed);


			WriteLine(new string('-', 20));
            WriteLine("Starting tests using config:");
			WriteLine($" - {nameof(config.TestAgainstAlreadyRunningElasticsearch)}: {config.TestAgainstAlreadyRunningElasticsearch}");
			WriteLine($" - {nameof(config.ElasticsearchVersion)}: {config.ElasticsearchVersion}");
			WriteLine($" - {nameof(config.ForceReseed)}: {config.ForceReseed}");
			WriteLine($" - {nameof(config.Mode)}: {config.Mode}");
			WriteLine($" - {nameof(config.Seed)}: {config.Seed}");
			if (config.Mode == TestMode.Integration)
			{
				WriteLine($" - {nameof(config.ClusterFilter)}: {config.ClusterFilter}");
				WriteLine($" - {nameof(config.TestFilter)}: {config.TestFilter}");

			}
			WriteLine($" - {nameof(config.RunIntegrationTests)}: {config.RunIntegrationTests}");
			WriteLine($" - {nameof(config.RunUnitTests)}: {config.RunUnitTests}");
			WriteLine($" - Random:");
			WriteLine($" \t- {nameof(config.Random.SourceSerializer)}: {config.Random.SourceSerializer}");
			WriteLine($" \t- {nameof(config.Random.TypedKeys)}: {config.Random.TypedKeys}");
			WriteLine($" \t- {nameof(config.Random.OldConnection)}: {config.Random.OldConnection}");
			WriteLine(new string('-', 20));


			return new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
		}
	}
}
