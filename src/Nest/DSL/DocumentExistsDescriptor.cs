﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elasticsearch.Net;
using Newtonsoft.Json;

namespace Nest
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public interface IDocumentExistsRequest : IRequest<DocumentExistsRequestParameters> { }

	public interface IDocumentExistsRequest<T> : IDocumentExistsRequest where T : class {}

	internal static class DocumentExistsPathInfo
	{
		public static void Update(ElasticsearchPathInfo<DocumentExistsRequestParameters> pathInfo, IDocumentExistsRequest request)
		{
			pathInfo.HttpMethod = PathInfoHttpMethod.HEAD;
		}
	}
	
	public partial class DocumentExistsRequest : DocumentPathBase<DocumentExistsRequestParameters>, IDocumentExistsRequest
	{
		protected override void UpdatePathInfo(IConnectionSettingsValues settings, ElasticsearchPathInfo<DocumentExistsRequestParameters> pathInfo)
		{
			DocumentExistsPathInfo.Update(pathInfo, this);
		}
	}
	
	public partial class DocumentExistsRequest<T> : DocumentPathBase<DocumentExistsRequestParameters, T>, IDocumentExistsRequest
		where T : class
	{
		protected override void UpdatePathInfo(IConnectionSettingsValues settings, ElasticsearchPathInfo<DocumentExistsRequestParameters> pathInfo)
		{
			DocumentExistsPathInfo.Update(pathInfo, this);
		}
	}

	[DescriptorFor("Exists")]
	public partial class DocumentExistsDescriptor<T>
		: DocumentPathDescriptor<DocumentExistsDescriptor<T>, DocumentExistsRequestParameters, T>, IDocumentExistsRequest
		where T : class
	{
		protected override void UpdatePathInfo(IConnectionSettingsValues settings, ElasticsearchPathInfo<DocumentExistsRequestParameters> pathInfo)
		{
			DocumentExistsPathInfo.Update(pathInfo, this);
		}
	}
}
