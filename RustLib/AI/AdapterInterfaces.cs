using System.Collections.Generic;
using UnityEngine;

namespace RustLib.AI
{
	public interface IEdgeAdapter
	{
		List<Edge> GetEdgesFrom(int node);
	}

	public interface IPointAdapter
	{
		Vector3 GetPoint(int point);
	}
}
