﻿//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution-ShareAlike 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by-sa/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using UnityEngine;
using AT_Utils;

namespace ThrottleControlledAvionics
{
	//adapted from MechJeb
	public class Coordinates : ConfigNodeObject, IEquatable<Coordinates>
	{
		[Persistent] public double Lat;
		[Persistent] public double Lon;
		[Persistent] public double Alt;

		/// <summary>
		/// When the <see cref="ThrottleControlledAvionics.Coordinates.SetAlt2Surface"/> is called, 
		/// this property is set to <c>true</c> if the point is on the water; otherwise it's <c>false</c>..
		/// </summary>
		public bool OnWater { get; private set; } = false;

		public Coordinates(double lat, double lon, double alt) 
		{ Lat = Utils.ClampAngle(lat); Lon = Utils.ClampAngle(lon); Alt = alt; }

		public Coordinates(Vector3 worldPos, CelestialBody body)
			: this(body.GetLatitude(worldPos), 
			       body.GetLongitude(worldPos), 
			       body.GetAltitude(worldPos)) {}

		public Coordinates(Vessel vsl) : this(vsl.latitude, vsl.longitude, vsl.altitude) {}

		public static Coordinates SurfacePoint(double lat, double lon, CelestialBody body)
		{
			var c = new Coordinates(lat, lon, 0);
			c.SetAlt2Surface(body);
			return c;
		}

		public static Coordinates SurfacePoint(Vector3 worldPos, CelestialBody body)
		{
			var c = new Coordinates(body.GetLatitude(worldPos), body.GetLongitude(worldPos), 0);
			c.SetAlt2Surface(body);
			return c;
		}

		public Coordinates Copy() { return new Coordinates(Lat, Lon, Alt); }

		public void SetAlt2Surface(CelestialBody body) 
		{ 
			Alt = SurfaceAlt(body, true);
			if(body.ocean && Alt < 0)
			{
				OnWater = true;
				Alt = 0;
			}
		}

		public static string AngleToDMS(double angle)
		{
			var d = (int)Math.Floor(Math.Abs(angle));
			var m = (int)Math.Floor(60 * (Math.Abs(angle) - d));
			var s = (int)Math.Floor(3600 * (Math.Abs(angle) - d - m / 60.0));
			return String.Format("{0:0}°{1:00}'{2:00}\"", Math.Sign(angle)*d, m, s);
		}

		public static string LatToDMS(double lat)
		{
			lat = Utils.CenterAngle(lat);
			return lat > 0? AngleToDMS(lat) + " N" : AngleToDMS(-lat) + " S";
		}

		public static string LonToDMS(double lon)
		{
			lon = Utils.CenterAngle(lon);
			return lon > 0? AngleToDMS(lon) + " E" : AngleToDMS(-lon) + " W";
		}

		public double SurfaceAlt(CelestialBody body, bool underwater=false) { return body.TerrainAltitude(Lat, Lon, underwater || !body.ocean); }
		public string Biome(CelestialBody body) { return ScienceUtil.GetExperimentBiome(body, Lat, Lon); }

		static Coordinates Search(CelestialBody body, Ray mouseRay)
		{
			if(body == null || body.pqsController == null) return null;
			Vector3d relSurfacePosition;
			Vector3d relOrigin = mouseRay.origin - body.position;
			double curRadius = body.pqsController.radiusMax;
			double lastRadius = 0;
			double error = 0;
			int loops = 0;
			float st = Time.time;
			while(loops < 50)
			{
				if(PQS.LineSphereIntersection(relOrigin, mouseRay.direction, curRadius, out relSurfacePosition))
				{
					var alt = body.pqsController.GetSurfaceHeight(relSurfacePosition);
					if(body.ocean && alt < body.Radius) alt = body.Radius;
					error = Math.Abs(curRadius - alt);
					if(error < (body.pqsController.radiusMax - body.pqsController.radiusMin) / 100)
						return Coordinates.SurfacePoint(body.position + relSurfacePosition, body);
					else
					{
						lastRadius = curRadius;
						curRadius = alt;
						loops++;
					}
				}
				else
				{
					if(loops == 0) break;
					else
					{ // Went too low, needs to try higher
						curRadius = (lastRadius * 9 + curRadius) / 10;
						loops++;
					}
				}
			}
			return null;
		}

		public static Coordinates GetAtPointer(CelestialBody body)
		{
			if(body == null) return null;
			var mouseRay = PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition);
			mouseRay.origin = ScaledSpace.ScaledToLocalSpace(mouseRay.origin);
			return Search(body, mouseRay);
		}

		public static Coordinates GetAtPointerInFlight()
		{
			var body = FlightGlobals.currentMainBody;
			var mouseRay = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
			RaycastHit raycast;
			return Physics.Raycast(mouseRay, out raycast, (float)body.Radius * 4f, 1 << 15)? 
				new Coordinates(body.GetLatitude(raycast.point), 
				                Utils.CenterAngle(body.GetLongitude(raycast.point)),
				                body.GetAltitude(raycast.point)) : 
				Search(body, mouseRay);
		}

		public override string ToString()
		{ return string.Format("{0} {1}", LatToDMS(Lat), LonToDMS(Lon)); }

		public string FullDescription(CelestialBody body)
		{ 
			return string.Format("{0}\nAlt: {1} {2}", this,
			                     Utils.formatBigValue((float)Alt, "m"), 
			                     Biome(body)); 
		}

		public string FullDescription(Vessel vsl) { return FullDescription(vsl.mainBody); }

		#region IEquatable implementation
		public bool Equals(Coordinates other)
		{ return other != null && Lat.Equals(other.Lat) && Lon.Equals(other.Lon) && Alt.Equals(other.Alt); }
		#endregion
	}
}
