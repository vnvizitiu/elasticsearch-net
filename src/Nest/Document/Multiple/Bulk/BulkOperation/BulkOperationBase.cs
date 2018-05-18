﻿using System;
using Elasticsearch.Net;

namespace Nest
{
	public abstract class BulkOperationBase : IBulkOperation
	{
		public IndexName Index { get; set; }
		public TypeName Type { get; set; }
		public Id Id { get; set; }
		public long? Version { get; set; }
		public VersionType? VersionType { get; set; }
		public Routing Routing { get; set; }
		[Obsolete("This property is no longer available in indices created in Elasticsearch 6.x and up")]
		public Id Parent { get; set; }
		public int? RetriesOnConflict { get; set; }

		string IBulkOperation.Operation => this.Operation;
		protected abstract string Operation { get; }

		Type IBulkOperation.ClrType => this.ClrType;
		protected abstract Type ClrType { get; }

		object IBulkOperation.GetBody() => this.GetBody();
		protected abstract object GetBody();

		Id IBulkOperation.GetIdForOperation(Inferrer inferrer) => this.GetIdForOperation(inferrer);
		Routing IBulkOperation.GetRoutingForOperation(Inferrer inferrer) => this.GetRoutingForOperation(inferrer);

		protected virtual Id GetIdForOperation(Inferrer inferrer) => this.Id ?? new Id(this.GetBody());
		protected virtual Routing GetRoutingForOperation(Inferrer inferrer) => this.Routing ?? new Routing(this.GetBody());
	}

	public abstract class BulkOperationDescriptorBase<TDescriptor, TInterface>
		: DescriptorBase<TDescriptor, TInterface>, IBulkOperation
		where TDescriptor : BulkOperationDescriptorBase<TDescriptor, TInterface>, TInterface, IBulkOperation
		where TInterface : class, IBulkOperation
	{
		string IBulkOperation.Operation => this.BulkOperationType;
		protected abstract string BulkOperationType { get; }

		Type IBulkOperation.ClrType => this.BulkOperationClrType;
		protected abstract Type BulkOperationClrType { get; }

		protected abstract object GetBulkOperationBody();

		/// <summary>
		/// Only used for bulk update operations but in the future might come in handy for other complex bulk ops.
		/// </summary>
		/// <returns></returns>
		object IBulkOperation.GetBody() => this.GetBulkOperationBody();

		Id IBulkOperation.GetIdForOperation(Inferrer inferrer) => this.GetIdForOperation(inferrer);
		Routing IBulkOperation.GetRoutingForOperation(Inferrer inferrer) => this.GetRoutingForOperation(inferrer);

		protected virtual Id GetIdForOperation(Inferrer inferrer) => Self.Id ?? new Id(this.GetBulkOperationBody());
		protected virtual Routing GetRoutingForOperation(Inferrer inferrer) => Self.Routing ?? new Routing(this.GetBulkOperationBody());

		IndexName IBulkOperation.Index { get; set; }
		TypeName IBulkOperation.Type { get; set; }
		Id IBulkOperation.Id { get; set; }
		long? IBulkOperation.Version { get; set; }
		VersionType? IBulkOperation.VersionType { get; set; }
		Routing IBulkOperation.Routing { get; set; }
		[Obsolete("This feature is no longer supported on indices created in Elasticsearch 6.x and up")]
		Id IBulkOperation.Parent { get; set; }
		int? IBulkOperation.RetriesOnConflict { get; set; }

		/// <summary>
		/// Manually set the index, default to the default index or the fixed index set on the bulk operation
		/// </summary>
		public TDescriptor Index(IndexName index) => Assign(a => a.Index = index);
		public TDescriptor Index<T>() => Assign(a => a.Index = typeof(T));

		/// <summary>
		/// Manualy set the type to get the object from, default to whatever
		/// T will be inferred to if not passed or the fixed type set on the parent bulk operation
		/// </summary>
		public TDescriptor Type(TypeName type) => Assign(a => a.Type = type);
		public TDescriptor Type<T>() => Assign(a => a.Type = typeof(T));

		/// <summary>
		/// Manually set the id for the newly created object
		/// </summary>
		public TDescriptor Id(Id id) => Assign(a => a.Id = id);

		public TDescriptor Version(long? version) => Assign(a => a.Version = version);

		public TDescriptor VersionType(VersionType? versionType) => Assign(a => a.VersionType = versionType);

		public TDescriptor Routing(Routing routing) => Assign(a => a.Routing = routing);

		[Obsolete("This feature is no longer supported on indices created in Elasticsearch 6.x and up")]
		public TDescriptor Parent(Id parent) => Assign(a => a.Parent = parent);
	}
}
