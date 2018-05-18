﻿using System;
using System.Collections.Generic;
using System.Threading;
using Bogus;
using Nest;

namespace Tests.Framework.MockData
{
	public class Shape
	{
		private static int _idState = 0;

		public int Id { get; set; }

		public IGeometryCollection GeometryCollection { get; set; }
		public IEnvelopeGeoShape Envelope { get; set; }
		public ICircleGeoShape Circle { get; set; }

		public static Faker<Shape> Generator { get; } =
			new Faker<Shape>()
				.UseSeed(TestClient.Configuration.Seed)
				.RuleFor(p => p.Id, p => Interlocked.Increment(ref _idState))
				.RuleFor(p => p.GeometryCollection, p =>
					new GeometryCollection(new List<IGeoShape>
					{
						GenerateRandomPoint(p),
						GenerateRandomMultiPoint(p),
						GenerateLineString(p),
						GenerateMultiLineString(p),
						GeneratePolygon(p),
						GenerateMultiPolygon(p)
					})
				)
				.RuleFor(p => p.Envelope, p => new EnvelopeGeoShape(new []
				{
					new GeoCoordinate(0, 0),
					new GeoCoordinate(45, 45)
				}))
				.RuleFor(p => p.Circle, p => new CircleGeoShape(GenerateGeoCoordinate(p), $"{p.Random.Int(1, 100)}km"))
			;

		public static IList<Shape> Shapes { get; } = Shape.Generator.Clone().Generate(10);

		private static IPointGeoShape GenerateRandomPoint(Faker p) =>
			new PointGeoShape(GenerateGeoCoordinate(p));

		private static IMultiPointGeoShape GenerateRandomMultiPoint(Faker p) =>
			new MultiPointGeoShape(GenerateGeoCoordinates(p,  p.Random.Int(1, 5)));

		private static ILineStringGeoShape GenerateLineString(Faker p) =>
			new LineStringGeoShape(GenerateGeoCoordinates(p, 3));

		private static IMultiLineStringGeoShape GenerateMultiLineString(Faker p)
		{
			var coordinates = new List<IEnumerable<GeoCoordinate>>();
			for (var i = 0; i < p.Random.Int(1, 5); i++)
				coordinates.Add(GenerateGeoCoordinates(p, 3));

			return new MultiLineStringGeoShape(coordinates);
		}

		private static IPolygonGeoShape GeneratePolygon(Faker p)
		{
			return new PolygonGeoShape(new List<IEnumerable<GeoCoordinate>>
			{
				GeneratePolygonCoordinates(p, GenerateGeoCoordinate(p))
			});
		}

		private static IMultiPolygonGeoShape GenerateMultiPolygon(Faker p)
		{
			return new MultiPolygonGeoShape(new List<IEnumerable<IEnumerable<GeoCoordinate>>>
			{
				new [] { GeneratePolygonCoordinates(p, GenerateGeoCoordinate(p)) }
			});
		}

		private static GeoCoordinate GenerateGeoCoordinate(Faker p) =>
			new GeoCoordinate(p.Address.Latitude(), p.Address.Longitude());

		private static IEnumerable<GeoCoordinate> GenerateGeoCoordinates(Faker p, int count)
		{
			var points = new List<GeoCoordinate>();

			for (var i = 0; i < count; i++)
				points.Add(GenerateGeoCoordinate(p));

			return points;
		}

		// adapted from https://gis.stackexchange.com/a/103465/30046
		private static IEnumerable<GeoCoordinate> GeneratePolygonCoordinates(Faker p, GeoCoordinate centroid, double maxDistance = 0.0002)
		{
			const int maxPoints = 20;
			var points = new List<GeoCoordinate>(maxPoints);
			double startingAngle = (int)(p.Random.Double() * (1d / 3) * Math.PI);
			var angle = startingAngle;
			for (var i = 0; i < maxPoints; i++)
			{
				var distance = p.Random.Double() * maxDistance;
				points.Add(new GeoCoordinate(centroid.Latitude + Math.Sin(angle)*distance, centroid.Longitude + Math.Cos(angle)*distance));
				angle = angle + p.Random.Double() * (2d / 3) * Math.PI;
				if (angle > 2 * Math.PI) break;
			}

			// close the polygon
			points.Add(points[0]);
			return points;
		}
	}
}
