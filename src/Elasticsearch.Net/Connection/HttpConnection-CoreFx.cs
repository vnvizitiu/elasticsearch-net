﻿#if DOTNETCORE
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.DecompressionMethods;

namespace Elasticsearch.Net
{
	internal class WebProxy : IWebProxy
	{
		private readonly Uri _uri;

		public WebProxy(Uri uri) { _uri = uri; }

		public ICredentials Credentials { get; set; }

		public Uri GetProxy(Uri destination) => _uri;

		public bool IsBypassed(Uri host) => host.IsLoopback;
	}

	public class HttpConnection : IConnection
	{
		private readonly object _lock = new object();
		private readonly ConcurrentDictionary<int, HttpClient> _clients = new ConcurrentDictionary<int, HttpClient>();

		private HttpClient GetClient(RequestData requestData)
		{
			var hashCode = requestData.GetHashCode();
			HttpClient client;
			if (this._clients.TryGetValue(hashCode, out client)) return client;
			lock (_lock)
			{
				if (this._clients.TryGetValue(hashCode, out client)) return client;

				var handler = CreateHttpClientHandler(requestData);

				client = new HttpClient(handler, false)
				{
					Timeout = requestData.RequestTimeout
				};

				client.DefaultRequestHeaders.ExpectContinue = false;

				this._clients.TryAdd(hashCode, client);
				return client;
			}

		}

		public virtual ElasticsearchResponse<TReturn> Request<TReturn>(RequestData requestData) where TReturn : class
		{
			var client = this.GetClient(requestData);
			var builder = new ResponseBuilder<TReturn>(requestData);
			try
			{
				var requestMessage = CreateHttpRequestMessage(requestData);
				var response = client.SendAsync(requestMessage).GetAwaiter().GetResult();
				requestData.MadeItToResponse = true;
				builder.StatusCode = (int)response.StatusCode;

				if (response.Content != null)
					builder.Stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
			}
			catch (HttpRequestException e)
			{
				builder.Exception = e;
			}

			return builder.ToResponse();
		}

		public virtual async Task<ElasticsearchResponse<TReturn>> RequestAsync<TReturn>(RequestData requestData, CancellationToken cancellationToken) where TReturn : class
		{
			var client = this.GetClient(requestData);
			var builder = new ResponseBuilder<TReturn>(requestData, cancellationToken);
			try
			{
				var requestMessage = CreateHttpRequestMessage(requestData);
				var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
				requestData.MadeItToResponse = true;
				builder.StatusCode = (int)response.StatusCode;

				if (response.Content != null)
					builder.Stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
			}
			catch (HttpRequestException e)
			{
				builder.Exception = e;
			}

			return await builder.ToResponseAsync().ConfigureAwait(false);
		}

		protected virtual HttpClientHandler CreateHttpClientHandler(RequestData requestData)
		{
			var handler = new HttpClientHandler
			{
				AutomaticDecompression = requestData.HttpCompression ? GZip | Deflate : None
			};

			if (!requestData.ProxyAddress.IsNullOrEmpty())
			{
				var uri = new Uri(requestData.ProxyAddress);
				var proxy = new WebProxy(uri);
				var credentials = new NetworkCredential(requestData.ProxyUsername, requestData.ProxyPassword);
				proxy.Credentials = credentials;
				handler.Proxy = proxy;
			}

			if (requestData.DisableAutomaticProxyDetection)
				handler.Proxy = null;

			return handler;
		}

		protected virtual HttpRequestMessage CreateHttpRequestMessage(RequestData requestData)
		{
			var request = this.CreateRequestMessage(requestData);
			SetBasicAuthenticationIfNeeded(request, requestData);
			return request;
		}

		protected virtual HttpRequestMessage CreateRequestMessage(RequestData requestData)
		{
			var method = ConvertHttpMethod(requestData.Method);
			var requestMessage = new HttpRequestMessage(method, requestData.Uri);

			foreach (string key in requestData.Headers)
			{
				requestMessage.Headers.TryAddWithoutValidation(key, requestData.Headers.GetValues(key));
			}

			requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(requestData.Accept));

			if (!requestData.RunAs.IsNullOrEmpty())
				requestMessage.Headers.Add("es-shield-runas-user", requestData.RunAs);

			var data = requestData.PostData;

			if (data != null)
			{
				var stream = requestData.MemoryStreamFactory.Create();
				requestMessage.Content = new StreamContent(stream);
				if (requestData.HttpCompression)
				{
					requestMessage.Content.Headers.Add("Content-Encoding", "gzip");
					requestMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
					requestMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
					using (var zipStream = new GZipStream(stream, CompressionMode.Compress, true))
						data.Write(zipStream, requestData.ConnectionSettings);
				}
				else
					data.Write(stream, requestData.ConnectionSettings);
				stream.Position = 0;
			}
			else
			{
				// Set content in order to set a Content-Type header.
				// Content gets diposed so can't be shared instance
				requestMessage.Content = new ByteArrayContent(new byte[0]);
			}

			requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(requestData.ContentType);

			return requestMessage;
		}

		protected virtual void SetBasicAuthenticationIfNeeded(HttpRequestMessage requestMessage, RequestData requestData)
		{
			string userInfo = null;
			if (!requestData.Uri.UserInfo.IsNullOrEmpty())
				userInfo = Uri.UnescapeDataString(requestData.Uri.UserInfo);
			else if (requestData.BasicAuthorizationCredentials != null)
				userInfo = requestData.BasicAuthorizationCredentials.ToString();
			if (!userInfo.IsNullOrEmpty())
			{
				var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(userInfo));
				requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
			}
		}


		private static System.Net.Http.HttpMethod ConvertHttpMethod(HttpMethod httpMethod)
		{
			switch (httpMethod)
			{
				case HttpMethod.GET: return System.Net.Http.HttpMethod.Get;
				case HttpMethod.POST: return System.Net.Http.HttpMethod.Post;
				case HttpMethod.PUT: return System.Net.Http.HttpMethod.Put;
				case HttpMethod.DELETE: return System.Net.Http.HttpMethod.Delete;
				case HttpMethod.HEAD: return System.Net.Http.HttpMethod.Head;
				default:
					throw new ArgumentException("Invalid value for HttpMethod", nameof(httpMethod));
			}
		}

		void IDisposable.Dispose() => this.DisposeManagedResources();

		protected virtual void DisposeManagedResources()
		{
			foreach (var c in _clients)
				c.Value.Dispose();
		}
	}
}
#endif
