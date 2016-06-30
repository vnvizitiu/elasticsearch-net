using System.Linq;
using ClientGenerator.Domain;

namespace ClientGenerator.Overrides.Descriptors
{
	public class ClearCachedRolesDescriptorOverrides : DescriptorOverridesBase
	{
		public override CsharpMethod PatchMethod(CsharpMethod method)
		{
			var part = method.Parts.First(p => p.Name == "name");
			part.ClrTypeNameOverride = "Names";
			return method;
		}
	}
}
