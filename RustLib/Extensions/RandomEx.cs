using System;
using System.Collections.Generic;

namespace RustLib.Extensions
{
	public static class RandomEx
	{
		public static double Between(this Random random, double min, double max)
		{
			double range = max - min;
			double r = random.NextDouble();
			r *= range;
			r += min;
			return r;
		}
	}
}
