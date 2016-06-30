using System;
using System.Collections.Generic;
using System.Linq;

namespace ClientGenerator.Overrides.Descriptors
{
	// ReSharper disable once UnusedMember.Global
	public class ClearCacheDescriptorOverrides : DescriptorOverridesBase
	{
		public override IEnumerable<string> SkipQueryStringParams => new []
		{
			"fielddata"
		};
	}
}
