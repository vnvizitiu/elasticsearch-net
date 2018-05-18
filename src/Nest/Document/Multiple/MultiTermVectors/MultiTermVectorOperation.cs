﻿using System;
using Elasticsearch.Net;
using Newtonsoft.Json;

namespace Nest
{
	public interface IMultiTermVectorOperation
	{
		[JsonProperty("_index")]
		IndexName Index { get; set; }
		[JsonProperty("_type")]
		TypeName Type { get; set; }
		[JsonProperty("_id")]
		Id Id { get; set; }
		[JsonProperty("doc")]
		[JsonConverter(typeof(SourceConverter))]
		object Document { get; set; }
		[JsonProperty("fields")]
		Fields StoredFields { get; set; }
		[JsonProperty("offsets")]
		bool? Offsets { get; set; }
		[JsonProperty("payloads")]
		bool? Payloads { get; set; }
		[JsonProperty("positions")]
		bool? Positions { get; set; }
		[JsonProperty("term_statistics")]
		bool? TermStatistics { get; set; }
		[JsonProperty("field_statistics")]
		bool? FieldStatistics { get; set; }
		[JsonProperty("filter")]
		ITermVectorFilter Filter { get; set; }
		[JsonProperty("version")]
		long? Version { get; set; }
		[JsonProperty("version_type")]
		VersionType? VersionType { get; set; }
		[JsonProperty("routing")]
		Routing Routing { get; set; }
	}

	public class MultiTermVectorOperation<T> : IMultiTermVectorOperation
		where T : class
	{

		public MultiTermVectorOperation(Id id)
		{
			this.Id = id;
			this.Index = typeof (T);
			this.Type = typeof (T);
		}

		public IndexName Index { get; set; }
		public TypeName Type { get; set; }
		public Id Id { get; set; }
		public object Document { get; set; }
		public Fields StoredFields { get; set; }
		public bool? Offsets { get; set; }
		public bool? Payloads { get; set; }
		public bool? Positions { get; set; }
		public bool? TermStatistics { get; set; }
		public bool? FieldStatistics { get; set; }
		public ITermVectorFilter Filter { get; set; }
		public long? Version { get; set; }
		public VersionType? VersionType { get; set; }

		private Routing _routing;
		public Routing Routing
		{
			get => _routing ?? (Document == null ? null : new Routing(Document));
			set => _routing = value;
		}
	}

	public class MultiTermVectorOperationDescriptor<T> : DescriptorBase<MultiTermVectorOperationDescriptor<T>, IMultiTermVectorOperation>, IMultiTermVectorOperation
		where T : class
	{
		IndexName IMultiTermVectorOperation.Index { get; set; } = typeof (T);
		TypeName IMultiTermVectorOperation.Type { get; set; } = typeof (T);
		Id IMultiTermVectorOperation.Id { get; set; }
		object IMultiTermVectorOperation.Document { get; set; }
		Fields IMultiTermVectorOperation.StoredFields { get; set; }
		bool? IMultiTermVectorOperation.Offsets { get; set; }
		bool? IMultiTermVectorOperation.Payloads { get; set; }
		bool? IMultiTermVectorOperation.Positions { get; set; }
		bool? IMultiTermVectorOperation.TermStatistics { get; set; }
		bool? IMultiTermVectorOperation.FieldStatistics { get; set; }
		ITermVectorFilter IMultiTermVectorOperation.Filter { get; set; }
		long? IMultiTermVectorOperation.Version { get; set; }
		VersionType? IMultiTermVectorOperation.VersionType { get; set; }

		private Routing _routing;
		Routing IMultiTermVectorOperation.Routing
		{
			get => _routing ?? (Self.Document == null ? null : new Routing(Self.Document));
			set => _routing = value;
		}

		public MultiTermVectorOperationDescriptor<T> StoredFields(Func<FieldsDescriptor<T>, IPromise<Fields>> fields) =>
			Assign(a => a.StoredFields = fields?.Invoke(new FieldsDescriptor<T>())?.Value);

		public MultiTermVectorOperationDescriptor<T> StoredFields(Fields fields) => Assign(a => a.StoredFields = fields);

		public MultiTermVectorOperationDescriptor<T> Id(Id id) => Assign(a=>a.Id = id);

		public MultiTermVectorOperationDescriptor<T> Document(T document) => Assign(a => a.Document = document);

		public MultiTermVectorOperationDescriptor<T> Offsets(bool? offsets = true) => Assign(a => a.Offsets = offsets);

		public MultiTermVectorOperationDescriptor<T> Payloads(bool? payloads = true) => Assign(a => a.Payloads = payloads);

		public MultiTermVectorOperationDescriptor<T> Positions(bool? positions = true) => Assign(a => a.Positions = positions);

		public MultiTermVectorOperationDescriptor<T> TermStatistics(bool? termStatistics = true) => Assign(a => a.TermStatistics = termStatistics);

		public MultiTermVectorOperationDescriptor<T> FieldStatistics(bool? fieldStatistics = true) => Assign(a => a.FieldStatistics = fieldStatistics);

		public MultiTermVectorOperationDescriptor<T> Filter(Func<TermVectorFilterDescriptor, ITermVectorFilter> filterSelector) =>
			Assign(a => a.Filter = filterSelector?.Invoke(new TermVectorFilterDescriptor()));

		public MultiTermVectorOperationDescriptor<T> Version(long? version) => Assign(a => a.Version = version);

		public MultiTermVectorOperationDescriptor<T> VersionType(VersionType? versionType) => Assign(a => a.VersionType = versionType);

		public MultiTermVectorOperationDescriptor<T> Routing(Routing routing) => Assign(a => a.Routing = routing);
	}
}
