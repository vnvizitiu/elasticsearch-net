﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Elasticsearch.Net;
using FluentAssertions;
using Nest;
using Newtonsoft.Json;
using Tests.Framework;
using Tests.Framework.MockData;
using Xunit;
#if DOTNETCORE
using System.Net.Http;
#endif

namespace Tests.ClientConcepts.LowLevel
{
	public class Connecting
	{
		/**== Connecting
		 * Connecting to Elasticsearch with `Elasticsearch.Net` is quite easy and there a few options to suit a number of different use cases.
		 *
		 * [[connection-strategies]]
		 * === Choosing the right Connection Strategy
		 * If you simply new an `ElasticLowLevelClient`, it will be a non-failover connection to `http://localhost:9200`
		 */
		public void InstantiateUsingAllDefaults()
		{
			var client = new ElasticLowLevelClient();
		}

		/**
		 * If your Elasticsearch node does not live at `http://localhost:9200` but instead lives somewhere else, for example, `http://mynode.example.com:8082/apiKey`, then
		 * you will need to pass in some instance of `IConnectionConfigurationValues`.
		 *
		 * The easiest way to do this is:
		 */
		public void InstantiatingASingleNodeClient()
		{
			var node = new Uri("http://mynode.example.com:8082/apiKey");
			var config = new ConnectionConfiguration(node);
			var client = new ElasticLowLevelClient(config);
		}

		/**
		 * This will still be a non-failover connection, meaning if that `node` goes down the operation will not be retried on any other nodes in the cluster.
		 *
		 * To get a failover connection we have to pass an <<connection-pooling, IConnectionPool>> instance instead of a `Uri`.
		 */
		public void InstantiatingAConnectionPoolClient()
		{
			var node = new Uri("http://mynode.example.com:8082/apiKey");
			var connectionPool = new SniffingConnectionPool(new[] { node });
			var config = new ConnectionConfiguration(connectionPool);
			var client = new ElasticLowLevelClient(config);
		}

		/**
		 * Here instead of directly passing `node`, we pass a <<sniffing-connection-pool, SniffingConnectionPool>>
		 * which will use our `node` to find out the rest of the available cluster nodes.
		 * Be sure to read more about <<connection-pooling, Connection Pooling and Cluster Failover>>.
		 *
		 * === Configuration Options
		 *
		 *Besides either passing a `Uri` or `IConnectionPool` to `ConnectionConfiguration`, you can also fluently control many more options. For instance:
		 */
		public void SpecifyingClientOptions()
		{
			var node = new Uri("http://mynode.example.com:8082/apiKey");
			var connectionPool = new SniffingConnectionPool(new[] { node });

			var config = new ConnectionConfiguration(connectionPool)
				.DisableDirectStreaming() //<1> Additional options are fluent method calls on `ConnectionConfiguration`
				.BasicAuthentication("user", "pass")
				.RequestTimeout(TimeSpan.FromSeconds(5));
		}
		/**
		 * The following is a list of available connection configuration options:
		 */
		public void AvailableOptions()
		{
			var config = new ConnectionConfiguration()
				.DisableAutomaticProxyDetection() // <1> Disable automatic proxy detection. When called, defaults to `true`.
				.EnableHttpCompression() // <2> Enable compressed request and responses from Elasticsearch (Note that nodes need to be configured to allow this. See the {ref_current}/modules-http.html[http module settings] for more info).
				.DisableDirectStreaming(); // <3> By default responses are deserialized directly from the response stream to the object you tell it to. For debugging purposes, it can be very useful to keep a copy of the raw response on the result object, which is what calling this method will do.

			var client = new ElasticLowLevelClient(config);
			var result = client.Search<SearchResponse<object>>(new { size = 12 });

			/** `.ResponseBodyInBytes` will only have a value if the client configuration has `DisableDirectStreaming` set */
			var raw = result.ResponseBodyInBytes;

			/**
			 * Please note that using `.DisableDirectStreaming` only makes sense if you need the mapped response **and** the raw response __at the same time__.
			 * If you need only a `string` response simply call
			 */
			var stringResult = client.Search<string>(new { });
			/**
			* and similarly, if you need only a `byte[]`
			*/
			var byteResult = client.Search<byte[]>(new { });

			/** other configuration options */
			config = config
				.GlobalQueryStringParameters(new NameValueCollection()) // <1> Allows you to set querystring parameters that have to be added to every request. For instance, if you use a hosted elasticserch provider, and you need need to pass an `apiKey` parameter onto every request.
				.Proxy(new Uri("http://myproxy"), "username", "pass") // <2> Sets proxy information on the connection.
				.RequestTimeout(TimeSpan.FromSeconds(4)) // <3> [[request-timeout]] Sets the global maximum time a connection may take. Please note that this is the request timeout, the builtin .NET `WebRequest` has no way to set connection timeouts (see http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.timeout(v=vs.110).aspx[the MSDN documentation on `HttpWebRequest.Timeout` Property]).
				.ThrowExceptions() // <4> As an alternative to the C/go like error checking on `response.IsValid`, you can instead tell the client to <<thrown-exceptions, throw exceptions>>.
				.PrettyJson() // <5> forces all serialization to be indented and appends `pretty=true` to all the requests so that the responses are indented as well
				.BasicAuthentication("username", "password"); // <6> sets the HTTP basic authentication credentials to specify with all requests.

			/**
			* NOTE: Basic authentication credentials can alternatively be specified on the node URI directly:
			*/
			var uri = new Uri("http://username:password@localhost:9200");
			var settings = new ConnectionConfiguration(uri);

			/**
			*...but this may become tedious when using connection pooling with multiple nodes.
			*
			* [[thrown-exceptions]]
			* === Exceptions
			* There are three categories of exceptions that may be thrown:
			*
			* `ElasticsearchClientException`:: These are known exceptions, either an exception that occurred in the request pipeline
			* (such as max retries or timeout reached, bad authentication, etc...) or Elasticsearch itself returned an error (could
			* not parse the request, bad query, missing field, etc...). If it is an Elasticsearch error, the `ServerError` property
			* on the response will contain the the actual error that was returned.  The inner exception will always contain the
			* root causing exception.
			*
			* `UnexpectedElasticsearchClientException`:: These are unknown exceptions, for instance a response from Elasticsearch not
			* properly deserialized.  These are usually bugs and {github}/issues[should be reported]. This exception also inherits from `ElasticsearchClientException`
			* so an additional catch block isn't necessary, but can be helpful in distinguishing between the two.
			*
			* Development time exceptions:: These are CLR exceptions like `ArgumentException`, `ArgumentOutOfRangeException`, etc.
			* that are thrown when an API in the client is misused.
			* These should not be handled as you want to know about them during development.
			*
			*/
		}

		/** === OnRequestCompleted
		 * You can pass a callback of type `Action<IApiCallDetails>` that can eavesdrop every time a response (good or bad) is created.
		 * If you have complex logging needs this is a good place to add that in.
		*/
		[U]
		public void OnRequestCompletedIsCalled()
		{
			var counter = 0;
			var client = TestClient.GetInMemoryClient(s => s.OnRequestCompleted(r => counter++));
			client.RootNodeInfo();
			counter.Should().Be(1);
			client.RootNodeInfoAsync();
			counter.Should().Be(2);
		}

		/**
		*`OnRequestCompleted` is called even when an exception is thrown
		*/
		[U]
		public void OnRequestCompletedIsCalledWhenExceptionIsThrown()
		{
			var counter = 0;
			var client = TestClient.GetFixedReturnClient(new { }, 500, s => s
				.ThrowExceptions()
				.OnRequestCompleted(r => counter++)
			);
			Assert.Throws<ElasticsearchClientException>(() => client.RootNodeInfo());
			counter.Should().Be(1);
			Assert.ThrowsAsync<ElasticsearchClientException>(() => client.RootNodeInfoAsync());
			counter.Should().Be(2);
		}

		/** [[complex-logging]]
		* === Complex logging with OnRequestCompleted
		* Here's an example of using `OnRequestCompleted()` for complex logging. Remember, if you would also like
		* to capture the request and/or response bytes, you also need to set `.DisableDirectStreaming()` to `true`
		*/
		[U]
		public async Task UsingOnRequestCompletedForLogging()
		{
			var list = new List<string>();
			var connectionPool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));

			var settings = new ConnectionSettings(connectionPool, new InMemoryConnection()) // <1> Here we use `InMemoryConnection`; in reality you would use another type of `IConnection` that actually makes a request.
				.DefaultIndex("default-index")
				.DisableDirectStreaming()
				.OnRequestCompleted(response =>
				{
					// log out the request and the request body, if one exists for the type of request
					if (response.RequestBodyInBytes != null)
					{
						list.Add(
							$"{response.HttpMethod} {response.Uri} \n" +
							$"{Encoding.UTF8.GetString(response.RequestBodyInBytes)}");
					}
					else
					{
						list.Add($"{response.HttpMethod} {response.Uri}");
					}

					// log out the response and the response body, if one exists for the type of response
					if (response.ResponseBodyInBytes != null)
					{
						list.Add($"Status: {response.HttpStatusCode}\n" +
								 $"{Encoding.UTF8.GetString(response.ResponseBodyInBytes)}\n" +
								 $"{new string('-', 30)}\n");
					}
					else
					{
						list.Add($"Status: {response.HttpStatusCode}\n" +
								 $"{new string('-', 30)}\n");
					}
				});

			var client = new ElasticClient(settings);

			var syncResponse = client.Search<object>(s => s
				.AllTypes()
				.AllIndices()
				.Scroll("2m")
				.Sort(ss => ss
					.Ascending(SortSpecialField.DocumentIndexOrder)
				)
			);

			list.Count.Should().Be(2);

			var asyncResponse = await client.SearchAsync<object>(s => s
				.AllTypes()
				.AllIndices()
				.Scroll("2m")
				.Sort(ss => ss
					.Ascending(SortSpecialField.DocumentIndexOrder)
				)
			);

			list.Count.Should().Be(4);
			list.ShouldAllBeEquivalentTo(new[]
			{
				"POST http://localhost:9200/_search?scroll=2m \n{\"sort\":[{\"_doc\":{\"order\":\"asc\"}}]}",
				"Status: 200\n------------------------------\n",
				"POST http://localhost:9200/_search?scroll=2m \n{\"sort\":[{\"_doc\":{\"order\":\"asc\"}}]}",
				"Status: 200\n------------------------------\n"
			});
		}

		public void ConfiguringSSL()
		{
			/**
			 * [[configuring-ssl]]
			 * === Configuring SSL
			 * SSL can be configured via the `ServerCertificateValidationCallback` property on either `ServerPointManager` or `HttpClientHandler`
			 * depending on which version of the .NET framework is in use.
			 *
			 * On the full .NET Framework, this must be done outside of the client using .NET's built-in
			 * http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager%28v=vs.110%29.aspx[ServicePointManager] class:
			 *
			 */
#if !DOTNETCORE
			ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;
#endif
			/**
			 * The bare minimum to make .NET accept self-signed SSL certs that are not in the Windows CA store would be to have the callback simply return `true`.
			 *
			 * However, this will accept **all** requests from the AppDomain to untrusted SSL sites,
			 * therefore **we recommend doing some minimal introspection on the passed in certificate.**
			 */
		}

#if DOTNETCORE
		/**
		 * If running on Core CLR, then a custom connection type must be created by deriving from `HttpConnection` and
		 * overriding the `CreateHttpClientHandler` method in order to set the `ServerCertificateCustomValidationCallback` property:
		*/
		public class SecureHttpConnection : HttpConnection
		{
			protected override HttpClientHandler CreateHttpClientHandler(RequestData requestData)
			{
				var handler = base.CreateHttpClientHandler(requestData);
				handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;
				return handler;
			}
		}
#endif

		/**=== Overriding Json.NET settings
		*
		* Overriding the default Json.NET behaviour in NEST is an expert behavior but if you need to get to the nitty gritty, this can be really useful.
		*/

		/**
		 * The easiest way is to create an instance of `SerializerFactory` that allows you to register a modification callback
		 * in the constructor
		 */
		public void EasyWay()
		{
			var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
			var connectionSettings = new ConnectionSettings(
				pool,
				new HttpConnection(),
				new SerializerFactory((jsonSettings, nestSettings) => jsonSettings.PreserveReferencesHandling = PreserveReferencesHandling.All));

			var client = new ElasticClient(connectionSettings);
		}


		/**
		 * A more involved and explicit way would be to implement your own JsonNetSerializer subclass.
		 *
		 * NOTE: this is subject to change in the next major release. NEST relies heavily on stateful deserializers (that have access to the original
		 * request) for specialized features such a covariant search results. This requirement leaks into this abstraction.
		 *
		 *
		 */
		public class MyJsonNetSerializer : JsonNetSerializer
		{
			public MyJsonNetSerializer(IConnectionSettingsValues settings)
				: base(settings, (s, csv) => s.PreserveReferencesHandling = PreserveReferencesHandling.All) //<1> Call this constructor if you only need access to `JsonSerializerSettings` without local state (properties on MyJsonNetSerializer)
			{
				OverwriteDefaultSerializers((s, cvs) => s.PreserveReferencesHandling = PreserveReferencesHandling.All); //<2> Call OverwriteDefaultSerializers if you need access to `JsonSerializerSettings` with local state
			}

			public int CallToModify { get; set; } = 0;

			public int CallToContractConverter { get; set; } = 0;

			protected override IList<Func<Type, JsonConverter>> ContractConverters => new List<Func<Type, JsonConverter>> //<3> You can inject contract resolved converters by implementing the ContractConverters property. This can be much faster then registering them on `JsonSerializerSettings.Converters`
			{
				t => {
					CallToContractConverter++;
					return null;
				}
			};
		}
	}
}
