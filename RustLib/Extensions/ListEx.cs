using System;
using System.Collections.Generic;

namespace RustLib.Extensions
{
	public static class ListEx
	{
		public static T RandomElement<T>(this List<T> list, Random random = null)
		{
			if (list.Count < 1)
				return default(T);

			if (random == null)
				random = new Random();

			int index = random.Next(0, list.Count - 1);
			T ret = list[index];

			return ret;
		}

		public static int NextValid<T>(this List<T> list, int current_index, Func<int, bool> is_valid = null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				current_index++;

				if (current_index >= list.Count)
					current_index = 0;

				if (is_valid != null && is_valid(current_index))
					return current_index;
			}

			return 0;
		}

		public static int PreviousValid<T>(this List<T> list, int current_index, Func<int, bool> is_valid = null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				current_index--;

				if (current_index < 0)
					current_index = list.Count - 1;

				if (is_valid != null && is_valid(current_index))
					return current_index;
			}

			return 0;
		}
	}
}
