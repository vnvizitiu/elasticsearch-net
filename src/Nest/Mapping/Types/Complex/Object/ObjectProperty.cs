using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nest
{
	[JsonObject(MemberSerialization.OptIn)]
	public interface IObjectProperty : ICoreProperty
	{
		[JsonProperty("dynamic")]
		Union<bool, DynamicMapping> Dynamic { get; set; }

		[JsonProperty("enabled")]
		bool? Enabled { get; set; }

		[JsonProperty("include_in_all")]
		bool? IncludeInAll { get; set; }

		[JsonProperty("properties", TypeNameHandling = TypeNameHandling.None)]
		IProperties Properties { get; set; }
	}

	public class ObjectProperty : CorePropertyBase, IObjectProperty
	{
		public ObjectProperty() : base("object") { }

		protected ObjectProperty(string type) : base(type) { }

		public Union<bool, DynamicMapping> Dynamic { get; set; }
		public bool? Enabled { get; set; }
		public bool? IncludeInAll { get; set; }
		public IProperties Properties { get; set; }
	}

	public class ObjectTypeDescriptor<TParent, TChild>
		: ObjectPropertyDescriptorBase<ObjectTypeDescriptor<TParent, TChild>, IObjectProperty, TParent, TChild>, IObjectProperty
		where TParent : class
		where TChild : class
	{
	}

	public abstract class ObjectPropertyDescriptorBase<TDescriptor, TInterface, TParent, TChild>
		: CorePropertyDescriptorBase<TDescriptor, TInterface, TParent>, IObjectProperty
		where TDescriptor : ObjectPropertyDescriptorBase<TDescriptor, TInterface, TParent, TChild>, TInterface
		where TInterface : class, IObjectProperty
		where TParent : class
		where TChild : class
	{
		internal TypeName _TypeName { get; set; }

		Union<bool, DynamicMapping> IObjectProperty.Dynamic { get; set; }
		bool? IObjectProperty.Enabled { get; set; }
		bool? IObjectProperty.IncludeInAll { get; set; }
		IProperties IObjectProperty.Properties { get; set; }

		protected ObjectPropertyDescriptorBase() : this("object") { }

		protected ObjectPropertyDescriptorBase(string type) : base(type)
		{
			_TypeName = TypeName.Create<TChild>();
		}

		public TDescriptor Dynamic(Union<bool, DynamicMapping> dynamic) =>
			Assign(a => a.Dynamic = dynamic);

		public TDescriptor Dynamic(bool dynamic = true) =>
			Assign(a => a.Dynamic = dynamic);

		public TDescriptor Enabled(bool enabled = true) =>
			Assign(a => a.Enabled = enabled);

		public TDescriptor IncludeInAll(bool includeInAll = true) =>
			Assign(a => a.IncludeInAll = includeInAll);

		public TDescriptor Properties(Func<PropertiesDescriptor<TChild>, IPromise<IProperties>> selector) =>
			Assign(a => a.Properties = selector?.Invoke(new PropertiesDescriptor<TChild>(a.Properties))?.Value);

		public TDescriptor AutoMap(IPropertyVisitor visitor = null, int maxRecursion = 0) =>
			Assign(a => a.Properties = a.Properties.AutoMap<TChild>(visitor, maxRecursion));

		public TDescriptor AutoMap(int maxRecursion) => AutoMap(null, maxRecursion);
	}
}
