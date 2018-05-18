﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Elasticsearch.Net;
using FluentAssertions;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Framework
{
	public class RoundTripper : SerializationTestBase
	{
		protected override object ExpectJson { get; }

		internal RoundTripper(object expected,
			Func<ConnectionSettings, ConnectionSettings> settings = null,
			ConnectionSettings.SourceSerializerFactory sourceSerializerFactory = null,
			IPropertyMappingProvider propertyMappingProvider = null)
		{
			this.ExpectJson = expected;
			this.ConnectionSettingsModifier = settings;
			this.SourceSerializerFactory = sourceSerializerFactory;
			this.PropertyMappingProvider = propertyMappingProvider;

			var expectedString = JsonConvert.SerializeObject(expected, NullValueSettings);
			this.ExpectedJsonJObject = JToken.Parse(expectedString);
		}

		public virtual void DeserializesTo<T>(Action<string, T> assert)
		{
			var json = (this.ExpectJson is string)
				? (string) ExpectJson
				: JsonConvert.SerializeObject(this.ExpectJson, NullValueSettings);

			T sut;
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
				sut = this.Client.RequestResponseSerializer.Deserialize<T>(stream);
			sut.Should().NotBeNull();
			assert("first deserialization", sut);

			var serialized = this.Client.RequestResponseSerializer.SerializeToString(sut);
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized)))
				sut = this.Client.RequestResponseSerializer.Deserialize<T>(stream);
			sut.Should().NotBeNull();
			assert("second deserialization", sut);
		}

		public void FromRequest(IResponse response) => ToSerializeTo(response.ApiCall.RequestBodyInBytes);
		public void FromRequest<T>(Func<IElasticClient, T> call) where T : IResponse => FromRequest(call(this.Client));
		public void FromResponse(IResponse response) => ToSerializeTo(response.ApiCall.ResponseBodyInBytes);
		public void FromResponse<T>(Func<IElasticClient, T> call) where T : IResponse => FromResponse(call(this.Client));
		public void ToSerializeTo(byte[] json) => ToSerializeTo(Encoding.UTF8.GetString(json));
		public void ToSerializeTo(string json)
		{
			if (this.ExpectJson == null) throw new Exception(json);

			if (this.ExpectedJsonJObject.Type != JTokenType.Array)
				CompareToken(json, JToken.FromObject(this.ExpectJson));
			else
				CompareMultiJson(json);
		}

		private void CompareMultiJson(string json)
		{
			var jArray = this.ExpectedJsonJObject as JArray;
			var lines = json.Split(new [] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			var zipped = jArray.Children<JObject>().Zip(lines, (j, s) => new {j, s}).ToList();
			foreach(var t in zipped)
				CompareToken(t.s, t.j);
			zipped.Count.Should().Be(lines.Count);
		}

		private void CompareToken(string json, JToken expected)
		{
			var actual = JToken.Parse(json);
			var sameJson = JToken.DeepEquals(expected, actual);
			if (sameJson) return;
			expected.ToString().Diff(actual.ToString(), "Expected serialization differs:");
		}

		public void WhenSerializingNoRoundtrip(object o) =>
			ToSerializeTo(this.Client.RequestResponseSerializer.SerializeToString(o));
		public virtual RoundTripper<T> WhenSerializing<T>(T actual)
		{
			var sut = this.AssertSerializesAndRoundTrips(actual);
			return new RoundTripper<T>(this.ExpectJson, sut);
		}

		public RoundTripper WhenInferringIdOn<T>(T project) where T : class
		{
			this.Client.Infer.Id<T>(project).Should().Be((string)this.ExpectJson);
			return this;
		}
		public RoundTripper WhenInferringRoutingOn<T>(T project) where T : class
		{
			this.Client.Infer.Routing<T>(project).Should().Be((string)this.ExpectJson);
			return this;
		}

		public RoundTripper ForField(Field field)
		{
			this.Client.Infer.Field(field).Should().Be((string)this.ExpectJson);
			return this;
		}

		public RoundTripper AsPropertiesOf<T>(T document) where T : class
		{
			var jo = JObject.Parse(this.Serialize(document));
			var serializedProperties = jo.Properties().Select(p => p.Name);
			var sut = this.ExpectJson as IEnumerable<string>;
			if (sut == null) throw new ArgumentException("Can not call AsPropertiesOf if sut is not IEnumerable<string>");

			sut.Should().BeEquivalentTo(serializedProperties);
			return this;
		}

	    public RoundTripper NoRoundTrip()
	    {
	        this.SupportsDeserialization = false;
	        return this;
	    }

		public static IntermediateChangedSettings WithConnectionSettings(Func<ConnectionSettings, ConnectionSettings> settings) =>
			new IntermediateChangedSettings(settings);

		public static IntermediateChangedSettings WithSourceSerializer(ConnectionSettings.SourceSerializerFactory factory) =>
			new IntermediateChangedSettings(s=>s.EnableDebugMode()).WithSourceSerializer(factory);

		public static RoundTripper Expect(object expected) =>  new RoundTripper(expected);

	}

	public class IntermediateChangedSettings
	{
		private readonly Func<ConnectionSettings, ConnectionSettings> _connectionSettingsModifier;
		private ConnectionSettings.SourceSerializerFactory _sourceSerializerFactory;
		private IPropertyMappingProvider _propertyMappingProvider;

		internal IntermediateChangedSettings(Func<ConnectionSettings, ConnectionSettings> settings)
		{
			this._connectionSettingsModifier = settings;
		}
		public IntermediateChangedSettings WithSourceSerializer(ConnectionSettings.SourceSerializerFactory factory)
		{
			this._sourceSerializerFactory = factory;
			return this;
		}
		public IntermediateChangedSettings WithPropertyMappingProvider(IPropertyMappingProvider propertyMappingProvider)
		{
			this._propertyMappingProvider = propertyMappingProvider;
			return this;
		}

		public RoundTripper Expect(object expected) =>
			new RoundTripper(expected, _connectionSettingsModifier, this._sourceSerializerFactory, this._propertyMappingProvider);
	}

	public class RoundTripper<T> : RoundTripper
	{
		protected T Sut { get; set;  }

		internal RoundTripper(object expected, T sut) : base(expected)
		{
			this.Sut = sut;
		}

		public RoundTripper<T> WhenSerializing(T actual)
		{
			Sut = this.AssertSerializesAndRoundTrips(actual);
			return this;
		}

		public RoundTripper<T> Result(Action<T> assert)
		{
			assert(this.Sut);
			return this;
		}

		public RoundTripper<T> Result<TOther>(Action<TOther> assert)
			where TOther : T
		{
			assert((TOther)this.Sut);
			return this;
		}
	}
}
