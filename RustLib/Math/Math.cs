using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using RustLib.Extensions;
using System;

namespace RustLib.Math3D
{
	public static class MathUtils
	{
		public static int LogarithmicSearch(Func<int, bool> is_valid, int start_point = 0)
		{
			int current = 1;

			while (is_valid(current + start_point))
				current = current * 2;

			int min = current / 2;
			int max = current;
			int middle;

			while (min < max - 1)
			{
				middle = (min + max) / 2;

				if (is_valid(middle + start_point))
					min = middle;
				else
					max = middle;
			}

			return min + start_point;
		}

		public static List<SpawnPoint> RankPointsSuitability(List<SpawnPoint> points, Dictionary<SpawnPoint, float> distances)
		{
			List<SpawnPoint> ret = new List<SpawnPoint>(points);

			ret.Sort(delegate (SpawnPoint lhs, SpawnPoint rhs)
			{
				float lhs_dist = distances[lhs];
				float rhs_dist = distances[rhs];
				float diff = lhs_dist - rhs_dist;
				diff *= 100.0f;
				return (int)(diff);
			});

			return ret;
		}

		public static Dictionary<SpawnPoint, float> AssignDistances(List<SpawnPoint> set_a, List<Vector3> set_b)
		{
			Dictionary<SpawnPoint, float> distances = new Dictionary<SpawnPoint, float>();
			float closest_distance = 0.0f;

			foreach (SpawnPoint point in set_a)
			{
				MathUtils.FindClosest(set_b, point.location.Vector3(), out closest_distance);
				distances[point] = closest_distance;
			}

			return distances;
		}

		public static Vector3 FindClosest(List<Vector3> set, Vector3 input, out float closest_distance)
		{
			if (set.Count < 1)
				throw new System.Exception("Input set was empty, no players to avoid?");

			Vector3 ret_val = set[0];
			closest_distance = Vector3.Distance(ret_val, input);

			foreach (Vector3 i in set)
			{
				float distance = Vector3.Distance(i, input);

				if (distance < closest_distance)
				{
					ret_val = i;
					closest_distance = distance;
				}
			}

			return ret_val;
		}

		public static Vector3 FurthestPointInAFromB(List<Vector3> set_a, List<Vector3> set_b)
		{
			Vector3 furthest_point = set_a[0];
			float furthest_distance = Vector3.Distance(furthest_point, set_b[0]);

			foreach (Vector3 i in set_a)
			{
				FindClosest(set_b, i, out float distance);

				if (distance > furthest_distance)
				{
					furthest_point = i;
					furthest_distance = distance;
				}
			}

			return furthest_point;
		}
	}

	public static class MathEx
	{
		public static Vec2 ToVec2(this Vector2 vec)
		{
			return new Vec2(vec);
		}

		public static Vec3 ToVec3(this Vector3 vec)
		{
			return new Vec3(vec);
		}

		public static Quat ToQuat(this Quaternion quat)
		{
			return new Quat(quat);
		}

		public static float CrossProduct(Vector2 a, Vector2 b)
		{
			float x = a.x * b.y;
			float y = a.y * b.x;
			float ret = x + y;
			return ret;
		}

		public static float CrossProduct(Vector2 origin, Vector2 a, Vector2 b)
		{
			a.x -= origin.x;
			a.y -= origin.y;
			b.x -= origin.x;
			b.y -= origin.y;

			float ret = CrossProduct(a, b);

			return ret;
		}

		public static bool IsPointWithinTriangle(Vector2 input, Vector2 t1, Vector2 t2, Vector2 t3)
		{
			float c1 = CrossProduct(t1, t2, input);
			float c2 = CrossProduct(t2, t3, input);
			float c3 = CrossProduct(t3, t1, input);

			bool all_pos = (c1 >= 0.0f && c2 >= 0.0f && c3 >= 0.0f);
			bool all_neg = (c1 <= 0.0f && c2 <= 0.0f && c3 <= 0.0f);

			return (all_pos || all_neg);
		}

		public static float GetHeightAtPoint(Vector2 point, Vector3 t1, Vector3 t2, Vector3 t3)
		{
			float d1 = Vector2.Distance(point, t1);
			float d2 = Vector2.Distance(point, t2);
			float d3 = Vector2.Distance(point, t3);

			float total = d1 + d2 + d3;

			float c1 = ((total - d1) / total) * t1.y;
			float c2 = ((total - d2) / total) * t2.y;
			float c3 = ((total - d3) / total) * t3.y;

			float height = c1 + c2 + c3;

			return height;
		}

		private static float Least(float a, float b, float c)
		{
			float ret = a;

			if (b < ret) ret = b;
			if (c < ret) ret = c;

			return ret;
		}

		private static float Greatest(float a, float b, float c)
		{
			float ret = a;

			if (b > ret) ret = b;
			if (c > ret) ret = c;

			return ret;
		}

		public static void Get2dBoundingBox(Vector2 t1, Vector2 t2, Vector2 t3, out Vector2 origin, out Vector2 size)
		{
			origin = new Vector2();
			size = new Vector2();

			origin.x = Least(t1.x, t2.x, t3.x);
			origin.y = Least(t1.y, t2.y, t3.y);

			size.x = Greatest(t1.x, t2.x, t3.x);
			size.y = Greatest(t1.y, t2.y, t3.y);

			size.x -= origin.x;
			size.y -= origin.y;
		}
	}

	public struct Vec2
	{
		public float x;
		public float y;

		// yes, this is intentional
		[JsonIgnore]
#pragma warning disable IDE1006 // Naming Styles
		public float z { get { return y; } set { y = value; } }
#pragma warning restore IDE1006 // Naming Styles

		public Vec2(float x, float y)
		{
			this.x = x;
			this.y = y;
		}

		public Vec2(Vector2 vec)
		{
			x = vec.x;
			y = vec.y;
		}

		public Vec2 Hash(float to_nearest)
		{
			Vec2 ret = this;

			ret.x = ret.x.NearestMultipleOf(to_nearest);
			ret.y = ret.y.NearestMultipleOf(to_nearest);

			return ret;
		}

		public Vector2 Vector2()
		{
			return new Vector2(x, y);
		}

		public override int GetHashCode()
		{
			Vector2 vec = Vector2();
			int hash = vec.GetHashCode();
			return hash;
		}

		public override bool Equals(object other)
		{
			bool is_vec2 = (other is Vec2);

			if (!is_vec2)
				return false;

			Vec2 v = (Vec2)other;

			bool x = (this.x == v.x);
			bool y = (this.y == v.y);

			bool ret = (x && y);

			return ret;
		}

		public override string ToString()
		{
			return $"{x}||{y}";
		}
	}

	public struct Vec3
	{
		public float x;
		public float y;
		public float z;

		public Vec3(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public Vec3(Vector3 vec)
		{
			x = vec.x;
			y = vec.y;
			z = vec.z;
		}

		public Vector3 Vector3()
		{
			return new Vector3(x, y, z);
		}

		public override int GetHashCode()
		{
			Vector3 vec = Vector3();
			int hash = vec.GetHashCode();
			return hash;
		}

		public override bool Equals(object other)
		{
			bool is_vec3 = (other is Vec3);

			if (!is_vec3)
				return false;

			Vec3 v = (Vec3)other;

			bool x = (this.x == v.x);
			bool y = (this.y == v.y);
			bool z = (this.z == v.z);

			bool ret = (x && y && z);

			return ret;
		}
	}

	public struct Quat
	{
		public float x;
		public float y;
		public float z;
		public float w;

		public Quat(Quaternion q)
		{
			x = q.x;
			y = q.y;
			z = q.z;
			w = q.w;
		}

		public Quaternion Quaternion()
		{
			return new Quaternion(x, y, z, w);
		}

		public override int GetHashCode()
		{
			Quaternion quat = Quaternion();
			int hash = quat.GetHashCode();
			return hash;
		}

		public override bool Equals(object other)
		{
			bool is_quat = (other is Quat);

			if (!is_quat)
				return false;

			Quat q = (Quat)other;

			bool x = (this.x == q.x);
			bool y = (this.y == q.y);
			bool z = (this.z == q.z);
			bool w = (this.w == q.w);

			bool ret = (x && y && z && w);

			return ret;
		}
	}

	public class SpawnPoint
	{
		public Vec3 location;
		public Quat rotation;

		public SpawnPoint(Vector3 point, Quaternion rotation)
		{
			this.location = new Vec3(point);
			this.rotation = new Quat(rotation);
		}

		public BasePlayer.SpawnPoint GetSpawnPoint()
		{
			BasePlayer.SpawnPoint ret = new BasePlayer.SpawnPoint();

			ret.pos = location.Vector3();
			ret.rot = rotation.Quaternion();

			return ret;
		}
	}

	public class AABB
	{
		public Vec3 location;
		public Vec3 size;

		public AABB()
		{

		}

		public AABB(Vector3 loc, Vector3 siz)
		{
			this.location = new Vec3(loc);
			this.size = new Vec3(siz);
		}

		public bool IsInside(Vector3 point)
		{
			Vector3 location = this.location.Vector3();
			Vector3 size = this.size.Vector3();

			bool x, y, z;

			x = (point.x > (location.x - (size.x / 2)) && point.x < (location.x + (size.x / 2)));
			y = (point.y > (location.y - (size.y / 2)) && point.y < (location.y + (size.y / 2)));
			z = (point.z > (location.z - (size.z / 2)) && point.z < (location.z + (size.z / 2)));

			return (x && y && z);
		}
	}
}
