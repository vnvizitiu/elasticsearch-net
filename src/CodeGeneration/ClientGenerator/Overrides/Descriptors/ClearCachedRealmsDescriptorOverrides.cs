using System.Linq;
using ClientGenerator.Domain;

namespace ClientGenerator.Overrides.Descriptors
{
	public class ClearCachedRealmsDescriptorOverrides : DescriptorOverridesBase
	{
		public override CsharpMethod PatchMethod(CsharpMethod method)
		{
			var part = method.Parts.First(p => p.Name == "realms");
			part.ClrTypeNameOverride = "Names";
			return method;
		}
	}
}
