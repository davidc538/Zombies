using RustLib.Extensions;
using RustLib.FileSystem;
using RustLib.Math3D;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RustLib.AI
{
	public class NavData : IEdgeAdapter, IPointAdapter
	{
		private float power_of_2;

		private int current_point_id = -1;

		private Dictionary<int, List<Edge>> links = new Dictionary<int, List<Edge>>();
		private Dictionary<int, Vec3> points = new Dictionary<int, Vec3>();
		private Dictionary<Vec2, List<int>> point_heights = new Dictionary<Vec2, List<int>>();

		public NavData() => power_of_2 = 1.0f;
		public NavData(int exponent) => power_of_2 = (float)Math.Pow(2.0f, exponent);

		public float Density()
		{
			return power_of_2;
		}

		public int GetNextPointID()
		{
			current_point_id++;
			return current_point_id;
		}

		public bool CanWalkStraight(int origin_id, int destination_id, float height_tolerance, PathfindingOptions options, Action<object> debug = null)
		{
			if (!points.ContainsKey(origin_id))
				throw new Exception($"origin point not found on mesh: {origin_id}");

			if (!points.ContainsKey(destination_id))
				throw new Exception($"destination point not found on mesh: {destination_id}");

			Vector3 origin = points[origin_id].Vector3();
			Vector3 destination = points[destination_id].Vector3();

			bool ret = CanWalkStraight(origin, destination, height_tolerance, options, debug);

			return ret;
		}

		public bool CanWalkStraight(Vector3 origin, Vector3 destination, float height_tolerance,
			PathfindingOptions options, Action<object> debug = null)
		{
			foreach ((Vector3 from, Vector3 to) in GetHashedPointLine(origin, destination))
			{
				bool is_linked = IsLinked(from, to, height_tolerance);
				bool is_obstructed = (options.obstruction_provider != null &&
					options.obstruction_provider.IsPointObstructed(to, null));

				if (!is_linked || is_obstructed)
					return false;
			}

			return true;
		}

		public IEnumerable<(Vector3, Vector3)> GetHashedPointLine(Vector3 origin, Vector3 destination)
		{
			Vector3 lerp_point = origin;
			Vector3 previous_lerp_point = lerp_point;

			bool HasReachedDestination(Vector3 current)
			{
				float distance = Vector3.Distance(current, destination);
				bool retval = (distance < power_of_2 / 2.0f);
				return retval;
			}

			while (!HasReachedDestination(lerp_point))
			{
				bool should_output = (previous_lerp_point.x != lerp_point.x && previous_lerp_point.z != lerp_point.z);

				if (should_output)
					yield return (previous_lerp_point, lerp_point);

				previous_lerp_point = lerp_point;

				lerp_point = Vector3.MoveTowards(lerp_point, destination, power_of_2);

				lerp_point.x = lerp_point.x.NearestMultipleOf(power_of_2);
				lerp_point.z = lerp_point.z.NearestMultipleOf(power_of_2);
			}

			yield return (previous_lerp_point, lerp_point);
		}

		public bool IsLinked(Vector3 origin, Vector3 destination, float height_tolerance)
		{
			Vec2 origin_hash = new Vec2(origin.x, origin.z).Hash(power_of_2);
			Vec2 destination_hash = new Vec2(destination.x, destination.z).Hash(power_of_2);

			if (!point_heights.ContainsKey(origin_hash))
				return false;

			if (!point_heights.ContainsKey(destination_hash))
				return false;

			List<int> origin_list = point_heights[origin_hash];
			List<int> destination_list = point_heights[destination_hash];

			foreach (int key1 in origin_list)
			{
				foreach (int key2 in destination_list)
				{
					bool is_linked = IsLinked(key1, key2);

					if (is_linked)
					{
						Vector3 stored_origin = points[key1].Vector3();
						Vector3 stored_destination = points[key2].Vector3();

						bool is_origin_within_tolerance = (Math.Abs(stored_origin.y - origin.y) < height_tolerance);
						bool is_destination_within_tolerance = (Math.Abs(stored_destination.y - destination.y) < height_tolerance);

						if (is_origin_within_tolerance && is_destination_within_tolerance)
							return true;
					}
				}
			}

			return false;
		}

		public bool IsLinked(int origin_id, int destination_id)
		{
			if (origin_id == destination_id)
				return true;

			if (!links.ContainsKey(origin_id))
				return false;

			List<Edge> edges = links[origin_id];

			bool ret = edges.Any(e => e.j == destination_id);

			return ret;
		}

		public float DiagonalCellDistance()
		{
			float ret = 1.5f * power_of_2;
			return ret;
		}

		public IEnumerable<Vector3> GetPointsInCircle(Vector3 center, float radius)
		{
			int multiple = (int)(radius / power_of_2);
			float distance = multiple * power_of_2;

			float x_min = center.x - distance;
			float z_min = center.z - distance;
			float x_max = center.x + distance;
			float z_max = center.z + distance;

			x_min = x_min.NearestMultipleOf(power_of_2);
			z_min = z_min.NearestMultipleOf(power_of_2);

			for (float x = x_min; x <= x_max; x += power_of_2)
			{
				for (float z = z_min; z <= z_max; z += power_of_2)
				{
					Vector3 current = new Vector3(x, center.y, z);
					distance = Vector3.Distance(current, center);

					if (distance < radius)
						yield return current;
				}
			}
		}

		public void AddWalkPoint(Vector3 point, Action<Vector3, Vector3, bool> visual_debug = null)
		{
			int inner_point = AddPoint(point);

			IEnumerable<Vector3> neighbouring_points = GetNeighbouringPoints(point);

			foreach (Vector3 neighbour in neighbouring_points)
			{
				int neighbour_point_id = GetPoint(neighbour, power_of_2);

				if (neighbour_point_id != -1)
				{
					bool is_already_linked = AddWalkLinks(inner_point, neighbour_point_id);

					visual_debug?.Invoke(point, neighbour, is_already_linked);
				}
			}
		}

		private IEnumerable<Vector3> GetNeighbouringPoints(Vector3 point)
		{
			float min_x = point.x - power_of_2;
			float min_z = point.z - power_of_2;

			float max_x = point.x + power_of_2;
			float max_z = point.z + power_of_2;

			for (float ix = min_x; ix <= max_x; ix += power_of_2)
			{
				for (float iz = min_z; iz <= max_z; iz += power_of_2)
				{
					bool is_center = (ix == point.x && iz == point.z);
					Vector3 outside_point = new Vector3(ix, point.y, iz);

					if (!is_center)
						yield return outside_point;
				}
			}
		}

		private bool AddWalkLinks(int from, int to)
		{
			Vector3 point1 = points[from].Vector3();
			Vector3 point2 = points[to].Vector3();

			float distance = Vector3.Distance(point1, point2);

			bool is_from_already_linked = AddWalkLink(from, to, distance);
			bool is_to_already_linked = AddWalkLink(to, from, distance);

			return (is_from_already_linked && is_to_already_linked);
		}

		private bool AddWalkLink(int from, int to, float distance)
		{
			if (!links.ContainsKey(from))
				links[from] = new List<Edge>();

			List<Edge> edges = links[from];

			bool is_already_linked = edges.Any(e => e.i == from && e.j == to);

			if (!is_already_linked)
				edges.Add(new Edge(from, to, distance, Edge.Type.Walk));

			return !is_already_linked;
		}

		public int GetPoint(Vector3 point, float max_diff = -1.0f, Action<object> debug = null)
		{
			Vec2 hash = new Vec2(point.x, point.z);

			hash.x = hash.x.NearestMultipleOf(power_of_2);
			hash.z = hash.z.NearestMultipleOf(power_of_2);

			if (!point_heights.ContainsKey(hash))
			{
				debug?.Invoke($"point not contained {hash}");
				return -1;
			}

			float closest_y = Single.MaxValue;
			int closest_id = -1;

			foreach (int i in point_heights[hash])
			{
				Vec3 vec = points[i];
				float y_diff = Math.Abs(point.y - vec.y);

				if (y_diff < closest_y)
				{
					closest_y = y_diff;
					closest_id = i;
				}
			}

			bool is_close_enough = (closest_y < max_diff || max_diff == -1.0f);

			if (is_close_enough)
				return closest_id;

			debug?.Invoke($"close enough point not found: {point}");
			return -1;
		}

		private int AddPoint(Vector3 point)
		{
			int ret = GetPoint(point, power_of_2);

			if (ret != -1)
				return ret;

			int point_id = GetNextPointID();
			points[point_id] = point.ToVec3();

			Vec2 hash = new Vec2(point.x, point.z);

			if (!point_heights.ContainsKey(hash))
				point_heights[hash] = new List<int>();

			point_heights[hash].Add(point_id);

			return point_id;
		}

		private void DebugDrawLine(int point_id, Action<Vector3, Vector3, int, int> visual_debug = null)
		{
			if (visual_debug == null)
				return;

			if (!links.ContainsKey(point_id))
				throw new Exception($"walk_links does not contain key: {point_id}");

			foreach (Edge edge in links[point_id])
			{
				int i_from = edge.i;
				int i_to = edge.j;

				if (!points.ContainsKey(i_from))
					throw new Exception($"points[{point_id}] does not contain key: {i_from} (i)");

				if (!points.ContainsKey(i_to))
					throw new Exception($"points[{point_id}] does not contain key: {i_to} (j)");

				Vector3 from = points[i_from].Vector3();
				Vector3 to = points[i_to].Vector3();

				visual_debug(from, to, i_from, i_to);
			}
		}

		public void DebugDrawInfo(Action<Vector3, Vector3, int, int> visual_debug)
		{
			foreach (int key in links.Keys)
				DebugDrawLine(key, visual_debug);
		}

		private HashSet<int> FindReachable(int from)
		{
			HashSet<int> reachable = new HashSet<int>();
			Queue<int> to_explore = new Queue<int>();

			to_explore.Enqueue(from);

			while (to_explore.Count > 0)
			{
				int current = to_explore.Dequeue();

				if (links.ContainsKey(current))
				{
					List<Edge> edges = links[current];

					foreach (Edge edge in edges)
					{
						if (!reachable.Contains(edge.j))
						{
							reachable.Add(edge.j);
							to_explore.Enqueue(edge.j);
						}
					}
				}
			}

			return reachable;
		}

		public NavData Prune(int from, Action<object> debug = null)
		{
			HashSet<int> reachable = FindReachable(from);
			Serialization prune = Serialization.FromNavData(this);

			prune.walk_links.RemoveWhere(edge => !reachable.Contains(edge.i));
			prune.walk_links.RemoveWhere(edge => !reachable.Contains(edge.j));

			List<int> to_remove = new List<int>();

			foreach (int key in prune.points.Keys)
				if (!reachable.Contains(key))
					to_remove.Add(key);

			foreach (int key in to_remove)
				prune.points.Remove(key);

			List<Serialization.Tuple> pruned_tuples = new List<Serialization.Tuple>();

			foreach (Serialization.Tuple current in prune.point_heights_serialization)
			{
				Serialization.Tuple new_tuple = new Serialization.Tuple
				{
					vec = current.vec,
					ints = new List<int>()
				};

				foreach (int key in current.ints)
					if (reachable.Contains(key))
						new_tuple.ints.Add(key);

				if (new_tuple.ints.Count > 0)
					pruned_tuples.Add(new_tuple);
			}

			prune.point_heights_serialization = pruned_tuples;
			return prune.ToNavData();
		}

		#region erasing
		public void Erase(Vector3 point, float max_y_difference, Action<Vector3, Vector3, int, int> visual_debug = null)
		{
			point.x = point.x.NearestMultipleOf(power_of_2);
			point.z = point.z.NearestMultipleOf(power_of_2);

			Vec2 hash = new Vec2(point.x, point.z);

			if (!point_heights.ContainsKey(hash))
				return;

			float min_y = point.y - max_y_difference;
			float max_y = point.y + max_y_difference;

			List<int> to_remove = new List<int>();

			foreach (int key in point_heights[hash])
			{
				bool is_above = (points[key].y > min_y);
				bool is_below = (points[key].y < max_y);

				if (is_above && is_below)
					to_remove.Add(key);
			}

			foreach (int key in to_remove)
			{
				DebugDrawLine(key, visual_debug);

				Erase(key);
			}
		}

		public void Erase(Vector3 point, float max_y_difference)
		{
			Vec2 hash = new Vec2(point.x, point.z);

			List<int> to_remove = new List<int>();

			foreach (int key in point_heights[hash])
			{
				Vector3 other_point = points[key].Vector3();
				bool is_close_enough = (Math.Abs(point.y - other_point.y) < max_y_difference);

				if (is_close_enough)
					to_remove.Add(key);
			}

			foreach (int key in to_remove)
				Erase(key);
		}

		public void Erase(int point_id)
		{
			Vector3 point = points[point_id].Vector3();
			Vec2 hash = new Vec2(point.x, point.z);

			List<Edge> linked_edges = links[point_id];

			foreach (Edge edge in linked_edges)
				EraseLinks(edge.j, point_id);

			links.Remove(point_id);
			points.Remove(point_id);
			point_heights[hash].Remove(point_id);

			if (point_heights[hash].Count < 1)
				point_heights.Remove(hash);
		}

		private void EraseLinks(int from, int to)
		{
			List<Edge> linked_edges = links[from];
			List<Edge> to_remove = new List<Edge>();

			foreach (Edge edge in linked_edges)
			{
				if (edge.j == to)
					to_remove.Add(edge);
			}

			foreach (Edge edge in to_remove)
				linked_edges.Remove(edge);

			if (links[from].Count < 1)
				links.Remove(from);
		}
		#endregion

		#region navigation
		public List<Edge> GetPath(int from, int to, PathfindingOptions options, Action<object> output = null)
		{
			if (!points.ContainsKey(to))
				throw new Exception("destination point not in points table");

			Vector3 target = points[to].Vector3();

			IComparer<Edge> comparator = new GraphSearch.ManhattanDistanceComparator(target, this, options);

			List<Edge> edges = GraphSearch.GetPathAStar(from, to, this, comparator, options, output);

			if (output != null) output("EdgeCount: " + edges.Count().ToString());

			return edges;
		}

		public List<Vector3> GetSmoothedPath(int from, int to, float height_tolerance, int max_points, PathfindingOptions options,
			 Action<object> debug = null)
		{
			int times_invoked = 0;
			Func<Vector3, bool> should_continue = (Vector3 point) =>
			{
				bool ret = (times_invoked < max_points);
				times_invoked++;
				return ret;
			};

			List<Vector3> path = GetSmoothedPath(from, to, height_tolerance, options, should_continue, debug);
			return path;
		}

		public List<Vector3> GetSmoothedPath(int from, int to, float height_tolerance, PathfindingOptions options, Func<Vector3, bool> should_continue = null, Action<object> debug = null)
		{
			List<Vector3> ret = new List<Vector3>();
			List<Edge> edges = GetPath(from, to, options, debug);

			if (edges.Count < 1)
			{
				debug?.Invoke($"edges was empty => from: {from}, to: {to}");
				ret.Add(GetPoint(from));
				return ret;
			}

			debug?.Invoke($"edge count: {edges.Count}");

			List<int> point_ids = GraphSearch.SmoothPath(edges, this, height_tolerance, options, should_continue, debug);

			debug?.Invoke($"smoothed point_id count: {point_ids.Count}");

			foreach (int point_id in point_ids)
			{
				Vector3 point = GetPoint(point_id);
				ret.Add(point);
			}

			return ret;
		}

		public PathFollower GetPathFollower(int from, int to, PathfindingOptions options, Action<object> output = null)
		{
			List<Edge> edges = GetPath(from, to, options, output);

			PathFollower follower = new PathFollower(edges, this);
			return follower;
		}

		public PathFollower GetPathFollower(Vector3 from, Vector3 to, PathfindingOptions options, Action<object> output = null)
		{
			from.x = from.x.NearestMultipleOf(power_of_2);
			from.z = from.z.NearestMultipleOf(power_of_2);

			to.x = to.x.NearestMultipleOf(power_of_2);
			to.z = to.z.NearestMultipleOf(power_of_2);

			int i_from = Closest(from);
			int i_to = Closest(to);

			bool is_valid = (i_from != -1 && i_to != -1);

			if (!is_valid)
				output?.Invoke($"{i_from}, {i_to}, {from}, {to}");

			if (i_from == -1)
				throw new Exception($"origin was not on the mesh {from}");

			if (i_to == -1)
				throw new Exception($"destination was not on the mesh {to}");

			List<Edge> edges = GetPath(i_from, i_to, options, output);

			PathFollower follower = new PathFollower(edges, this);
			return follower;
		}

		private int Closest(Vector3 point)
		{
			Vec2 hash = new Vec2(point.x, point.z);

			if (!point_heights.ContainsKey(hash))
				return -1;

			List<int> heights = point_heights[hash];

			int closest = heights[0];
			float closest_distance = Single.MaxValue;

			foreach (int key in heights)
			{
				Vector3 pos = points[key].Vector3();
				float distance = Vector3.Distance(pos, point);

				if (distance < closest_distance)
				{
					closest_distance = distance;
					closest = key;
				}
			}

			return closest;
		}
		#endregion

		#region interfaces
		public List<Edge> GetEdgesFrom(int node)
		{
			List<Edge> ret = null;

			if (links.ContainsKey(node))
				ret = links[node];

			return ret;
		}

		public Vector3 GetPoint(int point)
		{
			Vector3 ret = new Vector3();

			if (points.ContainsKey(point))
				ret = points[point].Vector3();

			return ret;
		}
		#endregion

		#region saving/loading
		private class Serialization
		{
			public class Tuple
			{
				public Vec2 vec;
				public List<int> ints;
			}

			public float power_of_2;
			public int current_point_index = -1;
			public HashSet<Edge> walk_links = new HashSet<Edge>();
			public Dictionary<int, Vec3> points = new Dictionary<int, Vec3>();
			public List<Tuple> point_heights_serialization = new List<Tuple>();

			public static Serialization FromNavData(NavData data)
			{
				Serialization srz = new Serialization();

				srz.power_of_2 = data.power_of_2;
				srz.current_point_index = data.current_point_id;
				srz.walk_links = new HashSet<Edge>();
				srz.points = data.points;
				srz.point_heights_serialization = new List<Serialization.Tuple>();

				foreach (int key in data.links.Keys)
				{
					List<Edge> edges = data.links[key];

					foreach (Edge edge in edges)
					{
						bool is_contained = srz.walk_links.Contains(edge);

						if (!is_contained)
							srz.walk_links.Add(edge);
					}
				}

				foreach (Vec2 key in data.point_heights.Keys)
				{
					List<int> set = data.point_heights[key];
					Serialization.Tuple tuple = new Serialization.Tuple { vec = key, ints = set };
					srz.point_heights_serialization.Add(tuple);
				}

				return srz;
			}

			public NavData ToNavData()
			{
				NavData ret = new NavData();

				ret.power_of_2 = power_of_2;
				ret.current_point_id = current_point_index;
				ret.points = points;
				ret.point_heights = new Dictionary<Vec2, List<int>>();
				ret.links = new Dictionary<int, List<Edge>>();

				foreach (Serialization.Tuple tuple in point_heights_serialization)
					ret.point_heights[tuple.vec] = tuple.ints;

				foreach (Edge edge in walk_links)
				{
					if (!ret.links.ContainsKey(edge.i))
						ret.links[edge.i] = new List<Edge>();

					ret.links[edge.i].Add(edge);
				}

				return ret;
			}
		}

		public static NavData Load(string filename)
		{
			filename = $"nav_data\\{filename}";
			Serialization srz = JSONFile.LoadJSON<Serialization>(filename);
			NavData ret = srz.ToNavData();
			return ret;
		}

		public void Save(string filename)
		{
			filename = $"nav_data\\{filename}";
			Serialization srz = Serialization.FromNavData(this);
			JSONFile.SaveJSON(filename, srz);
		}
		#endregion
	}
}
