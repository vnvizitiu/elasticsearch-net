﻿using System;
using System.Collections.Generic;
using Elasticsearch.Net;
using Newtonsoft.Json;

namespace Nest
{
	[Obsolete("Deprecated. Will be removed in the next major release. Use a percolate query with search api")]
	public interface IPercolateCountResponse : IResponse
	{
		long Took { get; }
		long Total { get; }
	}

	[Obsolete("Deprecated. Will be removed in the next major release. Use a percolate query with search api")]
	public interface IPercolateResponse : IPercolateCountResponse
	{
		IEnumerable<PercolatorMatch> Matches { get; }
	}

	[JsonObject]
	[Obsolete("Deprecated. Will be removed in the next major release. Use a percolate query with search api")]
	public class PercolateCountResponse : ResponseBase, IPercolateCountResponse
	{
		[JsonProperty(PropertyName = "took")]
		public long Took { get; internal set; }

		[JsonProperty(PropertyName = "total")]
		public long Total { get; internal set; }

		[JsonProperty(PropertyName = "_shards")]
		public ShardsMetaData Shards { get; internal set; }

		/// <summary>
		/// The individual error for separate requests on the _mpercolate API
		/// </summary>
		[JsonProperty(PropertyName = "error")]
		internal ServerError Error { get; set; }

		public override ServerError ServerError => this.Error ?? base.ServerError;
	}

	[JsonObject]
	[Obsolete("Deprecated. Will be removed in the next major release. Use a percolate query with search api")]
	public class PercolateResponse : PercolateCountResponse, IPercolateResponse
	{

		[JsonProperty(PropertyName = "matches")]
		public IEnumerable<PercolatorMatch> Matches { get; internal set; }
	}

	[Obsolete("Deprecated. Will be removed in the next major release. Use a percolate query with search api")]
	public class PercolatorMatch
	{
		[JsonProperty(PropertyName = "highlight")]
		public Dictionary<string, IList<string>> Highlight { get; set; }

		[JsonProperty(PropertyName = "_id")]
		public string Id { get; set; }

		[JsonProperty(PropertyName = "_index")]
		public string Index { get; set; }

		[JsonProperty(PropertyName = "_score")]
		public double Score { get; set; }
	}
}
