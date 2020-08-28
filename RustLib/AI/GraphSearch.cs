using RustLib.DataStructures;
using GameLib.Extensions;
using RustLib.Math3D;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustLib.AI
{
	public interface IObstructionProvider
	{
		bool IsPointObstructed(int point_id, Action<object> debug);
		bool IsPointObstructed(Vector3 point, Action<object> debug);
	}

	public class PathfindingOptions
	{
		public bool use_perlin_noise;

		public float perlin_x_offset;
		public float perlin_y_offset;
		public float perlin_multiplier;
		public float perlin_coord_multiplier;

		public IObstructionProvider obstruction_provider;

		public static PathfindingOptions Default()
		{
			PathfindingOptions options = new PathfindingOptions();

			options.use_perlin_noise = false;

			options.perlin_x_offset = 0.0f;
			options.perlin_y_offset = 0.0f;
			options.perlin_multiplier = 0.0f;
			options.perlin_coord_multiplier = 1.0f;

			options.obstruction_provider = null;

			return options;
		}

		public static PathfindingOptions RandomPerlin(System.Random random = null)
		{
			if (random == null)
				random = new System.Random();

			PathfindingOptions options = Default();

			options.use_perlin_noise = true;

			options.perlin_x_offset = (float)random.Between(-1000.0f, 1000.0f);
			options.perlin_y_offset = (float)random.Between(-1000.0f, 1000.0f);
			options.perlin_multiplier = (float)random.Between(0.0f, 15.0f);
			options.perlin_coord_multiplier = 1.0f;

			options.obstruction_provider = null;

			return options;
		}

		public static PathfindingOptions ObstructedPathfinding(IObstructionProvider obstruction_provider)
		{
			PathfindingOptions options = Default();
			options.obstruction_provider = obstruction_provider;
			return options;
		}
	}

	public class GraphSearch
	{
		public static List<int> SmoothPath(List<Edge> edges, NavData nav, float height_tolerance, PathfindingOptions options,
			 Func<Vector3, bool> should_continue, Action<object> debug = null)
		{
			List<int> unsmooth_point_ids = new List<int>();

			if (edges.Count < 1)
				return unsmooth_point_ids;

			unsmooth_point_ids.Add(edges[0].i);

			foreach (Edge edge in edges)
				unsmooth_point_ids.Add(edge.j);

			debug?.Invoke($"unsmooth point ids: {unsmooth_point_ids.Count}");

			List<int> smooth_point_ids = SmoothPath(unsmooth_point_ids, nav, height_tolerance, options, should_continue, debug);
			return smooth_point_ids;
		}

		public static List<int> SmoothPath(List<int> point_ids, NavData nav, float height_tolerance, PathfindingOptions options,
			 Func<Vector3, bool> should_continue = null, Action<object> debug = null)
		{
			List<int> smooth_point_ids = new List<int>();

			int current_index = 0;
			int previous_index;

			bool ShouldContinue()
			{
				bool has_reached_end_of_path = (current_index >= point_ids.Count - 1);
				Vector3 point = nav.GetPoint(point_ids[current_index]);
				bool should_continue_func = (should_continue != null && should_continue(point));

				debug?.Invoke($"WhileLoop ShouldContinue {has_reached_end_of_path} {should_continue_func}");

				if (has_reached_end_of_path || !should_continue_func)
					return false;

				return true;
			}

			while (ShouldContinue())
			{
				debug?.Invoke($"SmoothPath while loop: {current_index}, {point_ids.Count}");

				previous_index = current_index;

				current_index = FindLongestStraightLine(point_ids, nav, current_index, height_tolerance, options, debug);

				if (current_index == previous_index)
					current_index++;

				int point_id = point_ids[current_index];

				debug?.Invoke($"adding point_id: {point_id}");

				smooth_point_ids.Add(point_id);
			}

			return smooth_point_ids;
		}

		public static int FindLongestStraightLine(List<int> point_ids, NavData nav, int start_index, float height_tolerance,
			 PathfindingOptions options, Action<object> debug = null)
		{
			int origin_id = point_ids[start_index];

			debug?.Invoke($"FindLongestStraightLine: {origin_id}");

			int max = MathUtils.LogarithmicSearch((int i) =>
			{
				if (point_ids.Count <= i)
					return false;

				int destination_id = point_ids[i];
				bool can_walk = nav.CanWalkStraight(origin_id, destination_id, height_tolerance, options, debug);

				debug?.Invoke($"{i} cws test: {origin_id} => {destination_id} {can_walk}");

				return can_walk;
			}, start_index);

			return max;
		}

		public class EuclideanDistanceComparator : IComparer<Edge>
		{
			private Vector3 target;
			private IPointAdapter point_adapter;

			public EuclideanDistanceComparator(Vector3 target, IPointAdapter point_adapter)
			{
				this.target = target;
				this.point_adapter = point_adapter;
			}

			int IComparer<Edge>.Compare(Edge x, Edge y)
			{
				float i_heuristic, j_heuristic;
				float i_total, j_total;

				Vector3 i_v = point_adapter.GetPoint(x.j);
				Vector3 j_v = point_adapter.GetPoint(y.j);

				i_heuristic = Vector3.Distance(target, i_v);
				j_heuristic = Vector3.Distance(target, j_v);

				i_total = x.cost + i_heuristic;
				j_total = y.cost + j_heuristic;

				if (i_total < j_total) return -1;
				else if (j_total < i_total) return 1;
				else return 0;
			}
		}

		public class ManhattanDistanceComparator : IComparer<Edge>
		{
			private Vector3 target;
			private IPointAdapter point_adapter;
			private PathfindingOptions options;

			public ManhattanDistanceComparator(Vector3 target, IPointAdapter point_adapter, PathfindingOptions options)
			{
				this.target = target;
				this.point_adapter = point_adapter;
				this.options = options;
			}

			int IComparer<Edge>.Compare(Edge i, Edge j)
			{
				float i_heuristic, j_heuristic;
				float i_total, j_total;

				Vector3 i_v = point_adapter.GetPoint(i.j);
				Vector3 j_v = point_adapter.GetPoint(j.j);

				i_heuristic = ManhattanDistance(target, i_v);
				j_heuristic = ManhattanDistance(target, j_v);

				i_total = i.cost + i_heuristic;
				j_total = j.cost + j_heuristic;

				if (options.use_perlin_noise)
				{
					Vector3 i_coord = i_v + new Vector3(options.perlin_x_offset, 0, options.perlin_y_offset);
					Vector3 j_coord = j_v + new Vector3(options.perlin_x_offset, 0, options.perlin_y_offset);

					i_coord *= options.perlin_coord_multiplier;
					j_coord *= options.perlin_coord_multiplier;

					float i_perlin = Mathf.PerlinNoise(i_coord.x, i_coord.z);
					float j_perlin = Mathf.PerlinNoise(j_coord.x, j_coord.z);

					i_perlin *= options.perlin_multiplier;
					j_perlin *= options.perlin_multiplier;

					i_total += i_perlin;
					j_total += j_perlin;
				}

				if (i_total < j_total) return -1;
				else if (j_total < i_total) return 1;
				else return 0;
			}

			private static float ManhattanDistance(Vector3 first, Vector3 second)
			{
				float x_diff, y_diff, z_diff, total;

				x_diff = Math.Abs(first.x - second.x);
				y_diff = Math.Abs(first.y - second.y);
				z_diff = Math.Abs(first.z - second.z);

				total = x_diff + y_diff + z_diff;

				return total;
			}
		}

		private class BackTrackList<T> where T : Edge
		{
			private Dictionary<int, T> backtrack_data = new Dictionary<int, T>();

			public void Add(T edge)
			{
				backtrack_data[edge.j] = edge;
			}

			public List<T> BackTrack(int start, int final, Action<object> output = null)
			{
				List<T> ret_val = new List<T>();

				T current = backtrack_data[final];

				bool has_reached_beginning = false;

				while (!has_reached_beginning)
				{
					if (output != null) output($"backtracking: {current.j}");

					ret_val.Add(current);

					has_reached_beginning = (current.i == start);

					if (!has_reached_beginning)
						current = backtrack_data[current.i];
				}

				return ret_val;
			}
		}

		public static IEnumerable<Vector3> GetVecs(Dictionary<int, Vector3> points, IEnumerable<int> nodes)
		{
			List<Vector3> ret_val = new List<Vector3>();

			foreach (int i in nodes)
			{
				if (!points.ContainsKey(i))
					throw new Exception($"Points does not contain key for: {i}");

				Vector3 point = points[i];

				ret_val.Add(point);
			}

			return ret_val;
		}

		public static List<Edge> GetPathAStar(int start, int end, IEdgeAdapter edge_list, IComparer<Edge> heuristic_comparator,
			 PathfindingOptions options, Action<object> output = null)
		{
			if (output != null) output($"start: {start}, end: {end}");

			HashSet<int> explored = new HashSet<int>();

			PriorityQueue<Edge> edge_queue = new PriorityQueue<Edge>(heuristic_comparator);

			EnqueueNeighbours(edge_queue, edge_list, explored, 0.0f, start, options, output);

			BackTrackList<Edge> backtrack_list = new BackTrackList<Edge>();

			List<Edge> neighbouring_edges = edge_list.GetEdgesFrom(start);

			if (neighbouring_edges == null)
				throw new Exception($"neighbouring_edges was null, origin node: {start}");

			foreach (Edge edge in neighbouring_edges)
				backtrack_list.Add(edge);

			bool has_reached_destination = false;

			while (edge_queue.Count > 0 && !has_reached_destination)
			{
				Edge edge = edge_queue.Dequeue();

				output?.Invoke($"i: {edge.i}, j: {edge.j}, cost: {edge.cost}");

				backtrack_list.Add(edge);

				has_reached_destination = (edge.j == end);

				if (!has_reached_destination)
					EnqueueNeighbours(edge_queue, edge_list, explored, edge.cost, edge.j, options, output);
			}

			if (!has_reached_destination)
				return new List<Edge>();

			List<Edge> nodes = backtrack_list.BackTrack(start, end);

			nodes.Reverse();

			return nodes;
		}

		private static void EnqueueNeighbours(PriorityQueue<Edge> edge_queue, IEdgeAdapter edge_list, HashSet<int> explored,
			 float current_cost, int node, PathfindingOptions options, Action<object> output = null)
		{
			List<Edge> edges = edge_list.GetEdgesFrom(node);

			if (edges == null)
			{
				output?.Invoke($"edges was null, node: {node}");
				return;
			}

			foreach (Edge edge in edges)
			{
				bool is_node_explored = (explored.Contains(edge.j));
				bool is_node_obstructed = (options.obstruction_provider != null && options.obstruction_provider.IsPointObstructed(edge.j, output));

				if (output != null && is_node_obstructed)
					output($"node obstructed: {edge.j}");

				if (!is_node_explored && !is_node_obstructed)
				{
					Edge new_edge = new Edge(edge.i, edge.j, current_cost + edge.cost, edge.type);

					edge_queue.Enqueue(new_edge);

					output?.Invoke($"enqueueing i: {new_edge.i}, j: {new_edge.j}, cost: {new_edge.cost}");

					explored.Add(edge.j);
				}
			}
		}
	}
}
