// Reference: RustLib
using Oxide.Core;
using RustLib.AI;
using RustLib.Extensions;
using RustLib.Misc;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GameLib.Threading;
using System.Diagnostics;
using System.IO;

namespace Oxide.Plugins
{
	[Info("NavMeshPainter", "Sulley", 1.0)]
	[Description("NavMeshPainter")]
	public class NavMeshPainter : RustPlugin
	{
		// TerrainMeta.HeightMap.GetHeight(Vector3);
		// TerrainMeta.Path.Monuments
		// https://i.gyazo.com/95ff6801b669aa226cab346fbe7dce7b.png

		private ThreadPool thread_pool = new ThreadPool();

		void OnTick()
		{
			thread_pool.ProcessCallbacks((object obj) => { Puts(obj.ToString()); });

			lock (debug_info)
				while (debug_info.Count > 0)
					Puts($"{debug_info.Dequeue()}");
		}

		void Unload() => thread_pool.Shutdown();

		private const int exponent = -1;
		private Dictionary<ulong, NavData> builders = new Dictionary<ulong, NavData>();
		private Dictionary<ulong, Mode> modes = new Dictionary<ulong, Mode>();
		private Dictionary<ulong, bool> is_on_ground = new Dictionary<ulong, bool>();
		private Queue<object> debug_info = new Queue<object>();

		[ConsoleCommand("nv_clear")]
		void ccmdClear(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			ulong steamid = args.Player().userID;
			builders[steamid] = new NavData(exponent);
		}

		private void DrawLine(BasePlayer player, float time, Vector3 first, Vector3 second)
		{
			player.SendConsoleCommand("ddraw.line", time, Color.blue, first, second);
		}

		[ConsoleCommand("nv_showpoint")]
		void ccmdShowPoint(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData builder = GetNavData(player);

			int point_id = Convert.ToInt32(args.Args[0]);

			Vector3 point = builder.GetPoint(point_id);

			Utilities.DrawAABB(player, point, new Vector3(1.0f, 1.0f, 1.0f), Color.black, 5.0f);
			Utilities.DrawText(player, point + new Vector3(0, 3, 0), point_id.ToString());
		}

		[ConsoleCommand("nv_testpath")]
		void ccmdTestPath(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData builder = GetNavData(player);

			int origin_id = Convert.ToInt32(args.Args[0]);
			int destination_id = Convert.ToInt32(args.Args[1]);

			List<Edge> edges = builder.GetPath(origin_id, destination_id, PathfindingOptions.Default());

			List<Vector3> points = new List<Vector3>();

			foreach (Edge edge in edges)
			{
				Vector3 point = builder.GetPoint(edge.j);
				points.Add(point);
			}

			foreach (Vector3 point in points)
				Utilities.DrawAABB(player, point, new Vector3(0.5f, 0.5f, 0.5f), Color.black, 5.0f);
		}

		[ConsoleCommand("nv_tws")]
		void ccmdTestWalkStraight(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData nav = GetNavData(player);

			int origin_id = Convert.ToInt32(args.Args[0]);
			int destination_id = Convert.ToInt32(args.Args[1]);

			Stopwatch sw = new Stopwatch();
			sw.Start();

			bool can_walk_straight = nav.CanWalkStraight(origin_id, destination_id, 1.0f, PathfindingOptions.Default(), (object s) => { });

			sw.Stop();
			player.ChatMessage($"can walk straight test: {can_walk_straight} in {sw.ElapsedMilliseconds} ms, {sw.ElapsedTicks} ticks TPM ({TimeSpan.TicksPerMillisecond})");
		}

		[ConsoleCommand("nv_tsp")]
		void ccmdTestSmoothPath(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData nav = GetNavData(player);

			int origin_id = Convert.ToInt32(args.Args[0]);
			int destination_id = Convert.ToInt32(args.Args[1]);
			int max_points = Convert.ToInt32(args.Args[2]);

			Stopwatch sw = new Stopwatch();
			sw.Start();

			List<Vector3> points = nav.GetSmoothedPath(origin_id, destination_id, 0.1f, PathfindingOptions.Default());

			sw.Stop();

			player.ChatMessage($"GetSmoothedPath test count({points.Count}) {sw.ElapsedMilliseconds} ms, {sw.ElapsedTicks} ticks");

			player.SendConsoleCommand($"echo {origin_id}, {destination_id}, {points.Count}");

			foreach (Vector3 point in points)
			{
				Utilities.DrawAABB(player, point, new Vector3(0.5f, 0.5f, 0.5f), Color.black, 5.0f);
			}

			for (int i = 0; i < points.Count - 1; i++)
			{
				Vector3 origin = points[i];
				Vector3 destination = points[i + 1];

				DrawLine(player, 5.0f, origin, destination);
			}
		}

		[ConsoleCommand("nv_save")]
		void ccmdSave(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData nav = GetNavData(player);

			string filename = args.Args[0];

			player.ChatMessage($"saving to: {filename}");

			Action job = () => { nav.Save(filename); };

			Action callback = () => { player.ChatMessage($"saved: {filename}"); };

			thread_pool.EnqueueJob(job, callback);
		}

		[ConsoleCommand("nv_load")]
		void ccmdLoad(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();
			string filename = args.Args[0];

			player.ChatMessage($"loading nav file: {filename}");

			NavData nav = null;

			Action job = () => { nav = NavData.Load(filename); };
			Action callback = () =>
			{
				player.ChatMessage($"loaded nav file: {filename}");
				builders[player.userID] = nav;
			};

			thread_pool.EnqueueJob(job, callback);
		}

		[ConsoleCommand("nv_prune")]
		void ccmdPrune(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			int prune_from = Convert.ToInt32(args.Args[0]);

			NavData current = GetNavData(player);

			NavData pruned = null;

			Action job = () =>
			{
				pruned = current.Prune(prune_from, (obj) =>
					{
					 lock (debug_info)
						 debug_info.Enqueue(obj);
				 });
			};

			Action callback = () =>
			{
				builders[player.userID] = pruned;
			};

			thread_pool.EnqueueJob(job, callback);
		}

		[ConsoleCommand("nv_showall")]
		void ccmdShowAll(ConsoleSystem.Arg args)
		{
			float max_draw_distance = 10.0f;

			BasePlayer player = args.Player();
			if (!player.IsAdmin)
				return;

			ulong steamid = args.Player().userID;

			NavData builder = GetNavData(args.Player());

			float time = 2.0f;

			if (modes.ContainsKey(player.userID) && modes[player.userID] == Mode.Scalpel)
				max_draw_distance = 5.0f;

			builder.DebugDrawInfo((Vector3 from, Vector3 to, int i_from, int i_to) =>
			{
				float distance = Vector3.Distance(from, player.transform.position);

				bool args_not_null = (args.Args != null);
				bool should_ignore_distance = (args_not_null && (args.Args.Contains("inf") || args.Args.Contains("infinite")));
				bool is_close_enough = (distance < max_draw_distance);

				if (!is_close_enough && !should_ignore_distance)
					return;

				DrawLine(player, time, from, to);

				if (args.Args != null && args.Args.Contains("text"))
				{
					Utilities.DrawText(player, from, $"{i_from}", time);
					Utilities.DrawText(player, to, $"{i_to}", time);
				}
			});
		}

		NavData GetNavData(BasePlayer player)
		{
			if (!builders.ContainsKey(player.userID))
				builders[player.userID] = new NavData(exponent);

			return builders[player.userID];
		}

		// want to make sure we were on ground last tick as well as this tick or points
		// will appear above where they are supposed to when players land
		bool IsOnGround(BasePlayer player)
		{
			if (!is_on_ground.ContainsKey(player.userID))
				is_on_ground[player.userID] = false;

			bool ret = (player.IsOnGround() && is_on_ground[player.userID]);
			is_on_ground[player.userID] = player.IsOnGround();

			return ret;
		}

		void PaintCircle(BasePlayer player, float radius, float density, Action<Vector3> output)
		{
			int multiple = (int)(radius / density);
			float distance = multiple * density;

			Vector3 center = player.transform.position;

			center.y += 1.0f;

			float x_min = center.x - distance;
			float z_min = center.z - distance;
			float x_max = center.x + distance;
			float z_max = center.z + distance;

			x_min = x_min.NearestMultipleOfNthPowerOf2(exponent);
			z_min = z_min.NearestMultipleOfNthPowerOf2(exponent);

			for (float x = x_min; x <= x_max; x += density)
			{
				for (float z = z_min; z <= z_max; z += density)
				{
					Vector3 current = new Vector3(x, center.y, z);
					distance = Vector3.Distance(current, player.transform.position);

					if (distance < radius)
					{
						//int layer_mask = 2162688;
						//int layer_mask = 1218652417;
						int layer_mask = 7;
						// 1218652417
						RaycastHit hit;
						Vector3 direction = (current - center).normalized;
						Vector3 origin = player.transform.position + new Vector3(0.0f, 1.5f, 0.0f);
						Physics.Raycast(origin, direction, out hit);
						float cast_distance = Vector3.Distance(hit.point, center);

						if (cast_distance > distance + 1.0f)
						{
							Physics.Raycast(current, Vector3.down, out hit, layer_mask);

							// for some reason unknown to me, unity's raycast often returns points which
							// are not *precisely* along the input ray, so we'll toss out the x and z but keep the y
							Vector3 output_point = new Vector3(x, hit.point.y, z);
							output(output_point);
						}
					}
				}
			}
		}

		private float radius = 3.75f;

		[ConsoleCommand("nv_eraseid")]
		void ccmdEraseID(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			NavData builder = GetNavData(player);

			int point_id = Convert.ToInt32(args.Args[0]);

			player.ChatMessage($"Erasing: {point_id}");

			builder.Erase(point_id);
		}

		private enum Mode
		{
			None,
			Paint,
			Scalpel,
			ShowRaycast
		}

		[ConsoleCommand("nv_setmode")]
		void ccmdSetMode(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			string input = args.Args[0];

			Mode new_mode = (Mode)Enum.Parse(typeof(Mode), input);

			modes[player.userID] = new_mode;

			player.SendDebugMessage($"Mode set to {new_mode}");
		}

		[ConsoleCommand("nv_setradius")]
		void ccmdSetRadius(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			radius = Single.Parse(args.Args[0]);

			player.ChatMessage($"new radius: {radius}");
		}

		void OnPlayerTick(BasePlayer player)
		{
			if (!modes.ContainsKey(player.userID))
				return;

			Mode mode = modes[player.userID];

			if (mode == Mode.None)
				return;

			bool is_on_ground = IsOnGround(player);

			NavData builder = GetNavData(player);

			if (mode == Mode.Paint && is_on_ground)
			{
				PaintCircle(player, radius, builder.Density(), (Vector3 output) =>
				{
					Action<Vector3, Vector3, bool> visual_debug = (Vector3 from, Vector3 to, bool is_already_linked) =>
						 {
							  //DrawAABB(player, from + new Vector3(0, 1, 0), new Vector3(0.1f, 0.1f, 0.1f), Color.black, 0.1f);
							  //DrawLine(player, 0.1f, from, to);
						  };

					builder.AddWalkPoint(output, visual_debug);
				});
			}

			if (mode == Mode.ShowRaycast)
			{
				RaycastHit hit = GetForwardRaycastHit(player);
				Utilities.DrawAABB(player, hit.point, new Vector3(0.1f, 0.1f, 0.1f), Color.black, 0.1f);
			}

			if (mode == Mode.Scalpel)
			{
				RaycastHit hit = GetForwardRaycastHit(player);
				Vector3 hashed = hit.point;
				hashed.x = hashed.x.NearestMultipleOfNthPowerOf2(exponent);
				hashed.z = hashed.z.NearestMultipleOfNthPowerOf2(exponent);
				Utilities.DrawAABB(player, hashed, new Vector3(0.1f, 0.1f, 0.1f), Color.black, 0.1f);
			}
		}

		[ConsoleCommand("nv_erase")]
		void ccmdErase(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			RaycastHit hit = GetForwardRaycastHit(player);
			Vector3 hashed = hit.point;
			hashed.x = hashed.x.NearestMultipleOfNthPowerOf2(exponent);
			hashed.z = hashed.z.NearestMultipleOfNthPowerOf2(exponent);

			player.ChatMessage($"Erasing: {hashed}");

			NavData data = GetNavData(player);

			float max_erase_height_difference = 1.0f;

			data.Erase(hashed, max_erase_height_difference);
		}

		[ConsoleCommand("nv_addpoint")]
		void ccmdAddPoint(ConsoleSystem.Arg args)
		{
			if (!args.Player().IsAdmin)
				return;

			BasePlayer player = args.Player();

			RaycastHit hit = GetForwardRaycastHit(player);
			Vector3 hashed = hit.point;
			hashed.x = hashed.x.NearestMultipleOfNthPowerOf2(exponent);
			hashed.z = hashed.z.NearestMultipleOfNthPowerOf2(exponent);

			player.ChatMessage($"Adding: {hashed}");

			NavData data = GetNavData(player);

			Action<Vector3, Vector3, bool> visual_debug = (Vector3 from, Vector3 to, bool is_already_linked) =>
			{
				Color draw_color = Color.blue;

				if (is_already_linked)
					draw_color = Color.white;

				player.SendConsoleCommand("ddraw.line", 3.0f, Color.blue, from, to);
			};

			data.AddWalkPoint(hashed, visual_debug);
		}

		RaycastHit GetForwardRaycastHit(BasePlayer player, int layer_mask = 7)
		{
			RaycastHit hit;
			Vector3 start = player.eyes.position;
			Vector3 direction = player.eyes.HeadForward();
			Physics.Raycast(start, direction, out hit, layer_mask);
			return hit;
		}
	}
}