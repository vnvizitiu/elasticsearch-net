﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bogus;
using Tests.Framework.Versions;

namespace Tests.Framework.Configuration
{
	public interface ITestConfiguration
	{
		TestMode Mode { get; }
		ElasticsearchVersion ElasticsearchVersion { get; }
		string ClusterFilter { get; }
		string TestFilter { get; }
		bool ForceReseed { get; }
		bool TestAgainstAlreadyRunningElasticsearch { get; }

		int Seed { get; }

		bool RunIntegrationTests { get; }
		bool RunUnitTests { get; }

		RandomConfiguration Random { get; }
	}

	public class RandomConfiguration
	{
		public bool SourceSerializer { get; set; }
		public bool TypedKeys { get; set; }
		public bool OldConnection { get; set; }
	}

}
