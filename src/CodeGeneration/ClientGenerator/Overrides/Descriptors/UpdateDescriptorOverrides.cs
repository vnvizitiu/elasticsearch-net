using System.Collections.Generic;

namespace ClientGenerator.Overrides.Descriptors
{
	// ReSharper disable once UnusedMember.Global
	public class UpdateDescriptorOverrides : DescriptorOverridesBase
	{
		public override IEnumerable<string> SkipQueryStringParams => new []
		{
			"fields"
		};
	}
}
