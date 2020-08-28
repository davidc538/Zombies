namespace RustLib.AI
{
	public class Edge
	{
		public enum Type
		{
			Walk,
			Crouch,
			Climb_Ladder,
			Jump
		}

		public readonly int i, j;
		public readonly float cost;
		public readonly Type type;

		public Edge(int i, int j, float cost, Type type)
		{
			this.i = i;
			this.j = j;

			this.cost = cost;
			this.type = type;
		}

		public override int GetHashCode()
		{
			int ii = i;
			int ij = j << 16;

			int hash = ii | ij;
			hash = hash | (int)cost;
			hash = hash | (int)type;

			return hash;
		}

		public override bool Equals(object obj)
		{
			bool is_edge = (obj is Edge);

			if (!is_edge)
				return false;

			Edge other = (Edge)obj;

			bool bi = (this.i == other.i);
			bool bj = (this.j == other.j);
			bool bcost = (this.cost == other.cost);
			bool btype = (this.type == other.type);

			return (bi && bj && bcost && btype);
		}
	}
}
