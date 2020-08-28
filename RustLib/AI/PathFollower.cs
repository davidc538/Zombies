using RustLib.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RustLib.AI
{
	public class PathFollower
	{
		private readonly List<Edge> edges;
		private readonly IPointAdapter point_adapter;
		public readonly float total_cost;

		public PathFollower(List<Edge> edges, IPointAdapter point_adapter)
		{
			this.edges = edges;
			this.point_adapter = point_adapter;

			foreach (Edge edge in edges)
				total_cost = edge.cost;
		}

		public IEnumerable<Edge> GetEdges() => edges;

		public void GetTransform(float cost, int smoothing_level, float smoothing_distance, out Vector3 position,
			 out Vector3 look, Action<object> debug = null)
		{
			List<float> costs = new List<float>();

			for (int i = 0 - (smoothing_level / 2); i < (smoothing_level / 2); i++)
			{
				float smoothing_val = i * smoothing_distance;
				smoothing_val += cost;

				if (smoothing_val <= total_cost && smoothing_val >= 0.0f)
					costs.Add(smoothing_val);
			}

			debug?.Invoke(costs.Count);

			if (costs.Count < 1)
				costs.Add(0.0f);

			List<Vector3> positions = new List<Vector3>();
			List<Vector3> looks = new List<Vector3>();

			foreach (float current_cost in costs)
			{
				Vector3 pos;
				Vector3 look_vec;

				GetTransform(current_cost, out pos, out look_vec, debug);

				positions.Add(pos);
				looks.Add(look_vec);
			}

			position = Utilities.Average(positions);
			look = Utilities.Average(looks);
		}

		public void GetTransform(float cost, out Vector3 position, out Vector3 look, Action<object> debug = null)
		{
			float lerp_val = 0.0f;

			Edge previous;
			Edge edge = GetEdge(cost, out lerp_val, out previous, debug);

			if (edge == null)
				edge = edges.Last();

			Vector3 start = point_adapter.GetPoint(edge.i);
			Vector3 end = point_adapter.GetPoint(edge.j);

			float distance = Vector3.Distance(start, end);

			position = Vector3.Lerp(start, end, lerp_val);

			look = end - start;
		}

		private Edge GetEdge(float cost, out float lerp_val, out Edge previous_edge, Action<object> debug = null)
		{
			if (edges == null)
				throw new Exception("edge list was null");

			if (edges.Count < 1)
				throw new Exception("edge list was empty");

			if (cost < edges[0].cost)
			{
				previous_edge = null;

				Edge current_edge = edges[0];

				lerp_val = cost / current_edge.cost;

				return current_edge;
			}

			if (cost > total_cost)
				cost = total_cost;

			int min = 0;
			int max = edges.Count - 1;
			int middle = 0;
			Edge current = edges[0];

			while ((max - min) > 1)
			{
				middle = min + max;
				middle /= 2;

				current = edges[middle];

				if (current.cost < cost)
					min = middle;
				else if (current.cost >= cost)
					max = middle;
			}

			int index = max;

			current = edges[index];

			if (index > 0)
				previous_edge = edges[index - 1];
			else
				previous_edge = null;

			float current_cost = current.cost - previous_edge.cost;
			float remainder = cost - previous_edge.cost;

			lerp_val = remainder / current_cost;

			return current;
		}
	}
}
