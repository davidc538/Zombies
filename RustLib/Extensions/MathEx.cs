using System;

namespace RustLib.Extensions
{
	public static class MathEx
	{
		public static float NearestMultipleOfNthPowerOf2(this float input, int power_of_2)
		{
			float power = (float)Math.Pow(2.0f, power_of_2);
			float ret = input.NearestMultipleOf(power);
			return ret;
		}

		public static float NearestMultipleOf(this float input, float multiple_of)
		{
			/* // old and busted assed way
			float mod = Math.Abs(input % multiple_of);
			float lower = input - mod;
			float upper = lower + multiple_of;
			float lower_diff = Math.Abs(input - lower);
			float upper_diff = Math.Abs(input - upper);

			float ret = lower;

			if (upper_diff < lower_diff)
				 ret = upper;
			//*/


			// new hotness
			float ret = (float)RoundToNearest(input, multiple_of);

			return ret;
		}

		public static double RoundToNearest(double input, double multiple)
		{
			double ret = Math.Round(input / multiple) * multiple;
			return ret;
		}
	}
}
