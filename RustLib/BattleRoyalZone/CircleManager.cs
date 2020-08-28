using GameLib.Extensions;
using System;
using System.Collections.Generic;

namespace RustLib.BattleRoyaleZone
{
	public struct Circle
	{
		public float x;
		public float z;
		public float radius;
		public float time;

		public static Circle Lerp(Circle a, Circle b, float lerp_val)
		{
			Circle ret = new Circle();

			ret.x = Lerp(a.x, b.x, lerp_val);
			ret.z = Lerp(a.z, b.z, lerp_val);
			ret.radius = Lerp(a.radius, b.radius, lerp_val);
			ret.time = Lerp(a.time, b.time, lerp_val);

			return ret;
		}

		public bool IsPointOnEdge(float point_x, float point_z, float threshold)
		{
			float x_diff = Math.Abs(point_x - x);
			float z_diff = Math.Abs(point_z - z);
			float diff_squared = (x_diff * x_diff) + (z_diff * z_diff);
			float distance = (float)Math.Sqrt(diff_squared);
			float difference = Math.Abs(distance - radius);
			bool ret = (difference < threshold);
			return ret;
		}

		public bool IsPointInside(float point_x, float point_z)
		{
			float x_diff = Math.Abs(point_x - x);
			float z_diff = Math.Abs(point_z - z);
			float diff_squared = (x_diff * x_diff) + (z_diff * z_diff);
			bool ret = (diff_squared < radius * radius);
			return ret;
		}

		public static float Lerp(float a, float b, float lerp_val)
		{
			float ret = 0.0f;
			ret += lerp_val * a;
			ret += (1.0f - lerp_val) * b;
			return ret;
		}
	}

	public class CircleManager
	{
		public class RandomizationOptions
		{
			public float initial_radius;
			public float initial_time;
			public float center_x;
			public float center_z;
			public float radius_shrink_factor;
			public float time_shrink_factor;
			public float time_floor;
			public int how_many_circles;
			public int rng_seed;
			public bool should_randomize_offset_distance;

			public static RandomizationOptions Default()
			{
				RandomizationOptions ret = new RandomizationOptions();

				Random random = new Random();

				ret.initial_radius = 2000.0f;
				ret.initial_time = 300.0f;
				ret.center_x = 0;
				ret.center_z = 0;
				ret.radius_shrink_factor = 0.7f;
				ret.time_shrink_factor = 0.7f;
				ret.time_floor = 10.0f;
				ret.how_many_circles = 15;
				ret.rng_seed = random.Next();
				ret.should_randomize_offset_distance = false;

				return ret;
			}
		}

		public List<Circle> circles;

		private CircleManager() { }

		public static CircleManager Create(RandomizationOptions options)
		{
			CircleManager cm = new CircleManager();

			cm.circles = MakeCircles(options);

			return cm;
		}

		private static List<Circle> MakeCircles(RandomizationOptions options)
		{
			List<Circle> ret = new List<Circle>();

			float radius = options.initial_radius;
			float offset_factor = 1.0f - options.radius_shrink_factor;
			float time = options.initial_time;
			float center_x = options.center_x;
			float center_z = options.center_z;
			float angle;
			float offset_dist;
			float offset_x;
			float offset_z;

			Random random = new Random(options.rng_seed);

			for (int i = 0; i < options.how_many_circles; i++)
			{
				ret.Add(new Circle { x = center_x, z = center_z, radius = radius, time = time });
				angle = (float)random.Between(0.0f, 2.0f * Math.PI);

				if (options.should_randomize_offset_distance)
					offset_dist = (float)random.Between(0.0f, radius * offset_factor);
				else
					offset_dist = radius * offset_factor;

				radius *= options.radius_shrink_factor;
				offset_x = (float)Math.Sin(angle) * offset_dist;
				offset_z = (float)Math.Cos(angle) * offset_dist;
				center_x += offset_x;
				center_z += offset_z;
				time *= options.time_shrink_factor;

				if (time < options.time_floor)
					time = options.time_floor;
			}

			return ret;
		}

		public Circle GetOuterCircleAtTime(float seconds)
		{
			if (seconds > TotalSeconds())
				seconds = (float)Math.Floor(TotalSeconds());

			int index = OuterCircleIndexAtTime(seconds, out bool should_lerp, out float lerp_val);
			Circle ret;

			if (should_lerp && index < circles.Count - 1)
			{
				Circle a = circles[index];
				Circle b = circles[index + 1];
				ret = Circle.Lerp(a, b, lerp_val);
			}
			else
				ret = circles[index];

			return ret;
		}

		public Circle GetInnerCircleAtTime(float seconds)
		{
			if (seconds > TotalSeconds())
				seconds = (float)Math.Floor(TotalSeconds());

			int index = InnerCircleIndexAtTime(seconds);

			while (index >= circles.Count)
				index--;

			Circle ret = circles[index];
			return ret;
		}

		public int OuterCircleIndexAtTime(float seconds, out bool should_lerp, out float lerp_val)
		{
			int index = CircleIndexAtTime(seconds, out float seconds_remaining);

			should_lerp = (seconds_remaining < circles[index].time / 2);
			lerp_val = seconds_remaining / (circles[index].time / 2);

			return index;
		}

		public int InnerCircleIndexAtTime(float seconds)
		{
			int index = CircleIndexAtTime(seconds, out float seconds_remaining);

			if (seconds_remaining < circles[index].time / 2)
				index += 1;

			return index;
		}

		public int CircleIndexAtTime(float seconds, out float seconds_remaining)
		{
			for (int index = 0; index < circles.Count; index++)
			{
				Circle circle = circles[index];

				seconds -= circle.time;

				seconds_remaining = -seconds;

				if (seconds < 0)
					return index;
			}

			seconds_remaining = 0;
			return 0;
		}

		public float TotalSeconds()
		{
			float ret = 0;

			foreach (Circle c in circles)
				ret += c.time;

			return ret;
		}
	}
}
