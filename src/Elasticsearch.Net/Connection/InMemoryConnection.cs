﻿using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Elasticsearch.Net
{
	public class InMemoryConnection : IConnection
	{
		private readonly byte[] _responseBody;
		private readonly int _statusCode;
		private readonly Exception _exception;
		private readonly string _contentType;
		internal static readonly byte[] EmptyBody = Encoding.UTF8.GetBytes("");

		/// <summary>
		/// Every request will succeed with this overload, note that it won't actually return mocked responses
		/// so using this overload might fail if you are using it to test high level bits that need to deserialize the response.
		/// </summary>
		public InMemoryConnection()
		{
			_statusCode = 200;
		}

		public InMemoryConnection(byte[] responseBody, int statusCode = 200, Exception exception = null, string contentType = RequestData.MimeType)
		{
			_responseBody = responseBody;
			_statusCode = statusCode;
			_exception = exception;
			_contentType = contentType;
		}

		public virtual Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
			where TResponse : class, IElasticsearchResponse, new() =>
			this.ReturnConnectionStatusAsync<TResponse>(requestData, cancellationToken);

		public virtual TResponse Request<TResponse>(RequestData requestData)
			where TResponse : class, IElasticsearchResponse, new() =>
			this.ReturnConnectionStatus<TResponse>(requestData);

		protected TResponse ReturnConnectionStatus<TResponse>(RequestData requestData, byte[] responseBody = null, int? statusCode = null, string contentType = null)
			where TResponse : class, IElasticsearchResponse, new()
		{
			var body = responseBody ?? _responseBody;
			var data = requestData.PostData;
			if (data != null)
			{
				using (var stream = new MemoryStream())
				{
					if (requestData.HttpCompression)
						using (var zipStream = new GZipStream(stream, CompressionMode.Compress))
							data.Write(zipStream, requestData.ConnectionSettings);
					else
						data.Write(stream, requestData.ConnectionSettings);
				}
			}
			requestData.MadeItToResponse = true;

			var sc = statusCode ?? this._statusCode;
			Stream s = (body != null) ? new MemoryStream(body) : new MemoryStream(EmptyBody);
			return ResponseBuilder.ToResponse<TResponse>(requestData, _exception, sc, null, s, contentType ?? _contentType ?? RequestData.MimeType);
		}

		protected async Task<TResponse> ReturnConnectionStatusAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken, byte[] responseBody = null, int? statusCode = null, string contentType = null)
			where TResponse : class, IElasticsearchResponse, new()
		{
			var body = responseBody ?? _responseBody;
			var data = requestData.PostData;
			if (data != null)
			{
				using (var stream = new MemoryStream())
				{
					if (requestData.HttpCompression)
						using (var zipStream = new GZipStream(stream, CompressionMode.Compress))
							await data.WriteAsync(zipStream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);
					else
						await data.WriteAsync(stream, requestData.ConnectionSettings, cancellationToken).ConfigureAwait(false);
				}
			}
			requestData.MadeItToResponse = true;

			var sc = statusCode ?? this._statusCode;
			Stream s = (body != null) ? new MemoryStream(body) : new MemoryStream(EmptyBody);
			return await ResponseBuilder.ToResponseAsync<TResponse>(requestData, _exception, sc, null, s, contentType ?? _contentType, cancellationToken)
				.ConfigureAwait(false);
		}

		void IDisposable.Dispose() => DisposeManagedResources();

		protected virtual void DisposeManagedResources() { }
	}
}
