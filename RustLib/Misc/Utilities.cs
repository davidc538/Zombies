using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using Network;

namespace RustLib.Misc
{
	public class ItemInfo
	{
		public string icon;
		public string icon_small;
		public string short_name;
		public string display_name;
		public string ammo_type;
		public bool is_melee;
		public int item_id;
		public List<ulong> skins;
		public List<ulong> red_skins;
		public List<ulong> blue_skins;
	}

	public class ItemInfoManager
	{
		private static Dictionary<string, ItemInfo> item_infos;

		public static void Reload()
		{
			item_infos = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ItemInfo>>("rust_item_info");
		}

		public static string GetWeaponDisplayName(string shortname)
		{
			return item_infos[shortname].display_name;
		}

		public static bool IsMeleeWeapon(string weapon_shortname)
		{
			if (weapon_shortname == null) return false;

			if (!item_infos.ContainsKey(weapon_shortname)) return false;

			return item_infos[weapon_shortname].is_melee;
		}

		public static int GetItemCode(string item_shortname)
		{
			return ItemManager.FindItemDefinition(item_shortname).itemid;
		}

		public static ulong GetRandomSkinCode(string item_shortname, System.Random random)
		{
			List<ulong> skins = item_infos[item_shortname].skins;

			return skins[random.Next(skins.Count)];
		}

		public static ulong GetRandomBlueSkinCode(string item_shortname, System.Random random)
		{
			if (item_infos[item_shortname].blue_skins == null) return 0;

			List<ulong> skins = item_infos[item_shortname].blue_skins;

			return skins[random.Next(skins.Count)];
		}

		public static ulong GetRandomRedSkinCode(string item_shortname, System.Random random)
		{
			if (item_infos[item_shortname].red_skins == null) return 0;

			List<ulong> skins = item_infos[item_shortname].red_skins;

			return skins[random.Next(skins.Count)];
		}

		public static ItemInfo GetItemInfo(string shortname)
		{
			if (item_infos.ContainsKey(shortname))
				return item_infos[shortname];

			else return null;
		}
	}

	public class Utilities
	{
		public static void TeleportPlayer(BasePlayer player, Vector3 position)
		{
			TeleportPlayer(player, position, Quaternion.identity);
		}

		public static void TeleportPlayer(BasePlayer player, Vector3 position, Quaternion rotation)
		{
			if (player.net?.connection != null)
				player.ClientRPCPlayer(null, player, "StartLoading");
			player.SetParent(null, true, true);
			player.MovePosition(position);
			if (player.net?.connection != null)
				player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
			if (player.net?.connection != null)
				player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
			player.UpdateNetworkGroup();
			//player.UpdatePlayerCollider(true, false);
			player.SendNetworkUpdateImmediate(false);
			if (player.net?.connection == null) return;
			//TODO temporary for potential rust bug
			try
			{
				player.ClearEntityQueue(null);
			}
			catch
			{
			}
			player.SendFullSnapshot();
			/*
			player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
			player.transform.rotation = rotation;
			player.MovePosition(position);
			player.UpdateNetworkGroup();
			player.SendNetworkUpdateImmediate();
			player.ClearEntityQueue(null);
			player.ClientRPCPlayer(null, player, "StartLoading");
			player.SendFullSnapshot();

			Rigidbody body = player.GetComponent<Rigidbody>();
			body.angularVelocity = new Vector3(0, 0, 0);
			*/
		}

		public static void GiveAmmoStacks(BasePlayer player, string ammo_shortname, int num_stacks, int stack_size)
		{
			for (int i = 0; i < num_stacks; i++)
				player.inventory.containerMain.AddItem(ItemManager.FindItemDefinition(ammo_shortname), stack_size);
		}

		public static void GiveRocketLauncher(BasePlayer player, System.Random random)
		{
			ulong skin = ItemInfoManager.GetRandomSkinCode("rocket.launcher", random);

			Utilities.GiveWeapon(player, true, 649603450, skin);
			Utilities.GiveAmmoStacks(player, "ammo.rocket.basic", 1, 2);
		}

		public static void GiveWeapon(BasePlayer player, bool fill_magazine, int item_id, ulong skin_id)
		{
			Item gun = BuildItem(item_id, skin_id);

			if (fill_magazine)
			{
				BaseEntity gun_held_entity = gun.GetHeldEntity();

				BaseProjectile projectile = (BaseProjectile)gun_held_entity;

				projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;

				projectile.SendNetworkUpdateImmediate();
			}

			player.inventory.GiveItem(gun, player.inventory.containerBelt);
		}

		// true for red, false for blue
		public static Item BuildItemColoredRandomSkin(string shortname, System.Random random, bool team_color)
		{
			int item_code = ItemInfoManager.GetItemCode(shortname);
			ulong skin;

			if (team_color)
				skin = ItemInfoManager.GetRandomRedSkinCode(shortname, random);
			else
				skin = ItemInfoManager.GetRandomBlueSkinCode(shortname, random);

			return BuildItem(item_code, skin);
		}

		public static Item BuildItemRandomSkin(string shortname, System.Random random)
		{
			int item_code = ItemInfoManager.GetItemCode(shortname);
			ulong skin = ItemInfoManager.GetRandomSkinCode(shortname, random);

			return BuildItem(item_code, skin);
		}

		public static void GiveItem(BasePlayer player, int item_code, ulong skin_code)
		{
			player.inventory.GiveItem(BuildItem(item_code, skin_code), player.inventory.containerMain);
		}

		public static Item BuildItem(int item_code, ulong skin)
		{
			return ItemManager.CreateByItemID(item_code, 1, skin);
		}

		public static void AddHealth(BasePlayer player, float health)
		{
			float new_health = player.health + health;

			if (new_health > 100.0f)
				new_health = 100.0f;

			player.health = new_health;
		}

		public static void FillHealth(BasePlayer player)
		{
			player.health = 100.0f;

			player.metabolism.calories.value = player.metabolism.calories.max;
			player.metabolism.hydration.value = player.metabolism.hydration.max;
		}

		public static void ShootRandomRocketFrom(Vector3 from, float dispersion)
		{
			Vector3 dir = new Vector3();

			dir.y = 100.0f;
			dir.x = UnityEngine.Random.Range(-dispersion, dispersion);
			dir.z = UnityEngine.Random.Range(-dispersion, dispersion);

			dir = dir.normalized * 20;

			ShootRocket(from, dir);
		}

		public static void ShootRocket(Vector3 from, Vector3 direction)
		{
			ItemDefinition it_def = ItemManager.FindItemDefinition("ammo.rocket.basic");

			ItemModProjectile component = it_def.GetComponent<ItemModProjectile>();
			BaseEntity entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath,
				 from, new Quaternion(), true);

			entity.SendMessage("InitializeVelocity", (object)(direction));

			entity.Spawn();
		}

		public static OreHotSpot CreateOreFlare(Vector3 position)
		{
			string prefab = "assets/prefabs/misc/orebonus/orebonus_generic.prefab";
			OreHotSpot ent = (OreHotSpot)GameManager.server.CreateEntity(prefab, position, new Quaternion(), true);
			ent.visualDistance = 100f;
			ent.Spawn();

			return ent;
		}

		public static void SetLights(bool on)
		{
			BaseOven[] ovens = Resources.FindObjectsOfTypeAll<BaseOven>();

			foreach (BaseOven oven in ovens)
			{
				if (oven == null || oven.inventory == null) continue;

				oven.inventory.AddItem(ItDef("lowgradefuel"), 1);
				oven.inventory.AddItem(ItDef("wood"), 1);

				if (on)
					oven.StartCooking();
				else
					oven.StopCooking();
			}
		}

		public static ItemDefinition ItDef(string shortname)
		{
			return ItemManager.FindItemDefinition(shortname);
		}

		public static void DrawText(BasePlayer player, Vector3 point, string name, float time = 10f, int size = 30)
		{
			player.SendConsoleCommand("ddraw.text", time, Color.green, point + new Vector3(0, 1.5f, 0), $"<size={size}>{name}</size>");
		}

		public static void DrawAABB(BasePlayer player, Vector3 location, Vector3 size, Color color, float time)
		{
			Vector3 left_bottom_front = location - (size / 2.0f);
			Vector3 right_top_back = location + (size / 2.0f);

			Vector3 right_bottom_front = new Vector3(right_top_back.x, left_bottom_front.y, left_bottom_front.z);
			Vector3 left_top_front = new Vector3(left_bottom_front.x, right_top_back.y, left_bottom_front.z);
			Vector3 right_top_front = new Vector3(right_top_back.x, right_top_back.y, left_bottom_front.z);

			Vector3 right_bottom_back = new Vector3(right_top_back.x, left_bottom_front.y, right_top_back.z);
			Vector3 left_top_back = new Vector3(left_bottom_front.x, right_top_back.y, right_top_back.z);
			Vector3 left_bottom_back = new Vector3(left_bottom_front.x, left_bottom_front.y, right_top_back.z);

			player.SendConsoleCommand("ddraw.line", time, color, left_bottom_front, right_bottom_front);
			player.SendConsoleCommand("ddraw.line", time, color, right_bottom_front, right_bottom_back);
			player.SendConsoleCommand("ddraw.line", time, color, right_bottom_back, left_bottom_back);
			player.SendConsoleCommand("ddraw.line", time, color, left_bottom_back, left_bottom_front);

			player.SendConsoleCommand("ddraw.line", time, color, left_bottom_front, left_top_front);
			player.SendConsoleCommand("ddraw.line", time, color, right_bottom_front, right_top_front);
			player.SendConsoleCommand("ddraw.line", time, color, right_bottom_back, right_top_back);
			player.SendConsoleCommand("ddraw.line", time, color, left_bottom_back, left_top_back);

			player.SendConsoleCommand("ddraw.line", time, color, left_top_front, right_top_front);
			player.SendConsoleCommand("ddraw.line", time, color, right_top_front, right_top_back);
			player.SendConsoleCommand("ddraw.line", time, color, right_top_back, left_top_back);
			player.SendConsoleCommand("ddraw.line", time, color, left_top_back, left_top_front);
		}

		public static void DrawAABB(BasePlayer player, Vector3 location, Vector3 size, float time = 30.0f)
		{
			DrawAABB(player, location, size, Color.blue, time);
		}

		public static List<Vector3> MakeCircle(float radius, int num_points, float offset_angle = 0.0f)
		{
			List<Vector3> ret_val = new List<Vector3>();

			if (num_points < 1) return ret_val;

			List<float> angles = MakeAnglesForCircle(num_points);

			foreach (float angle in angles)
			{
				Vector3 vec = MakeVectorForAngle(angle + offset_angle, radius);

				ret_val.Add(vec);
			}

			return ret_val;
		}

		public static Vector3 MakeVectorForAngle(float angle, float radius)
		{
			Vector3 vec = new Vector3();

			vec.x = Mathf.Sin(angle);
			vec.z = Mathf.Cos(angle);

			vec = vec * radius;

			return vec;
		}

		public static List<float> MakeAnglesForCircle(int num_points)
		{
			List<float> ret_val = new List<float>();

			for (int i = 0; i < num_points; i++)
				ret_val.Add(((float)i / (float)num_points) * (float)Math.PI * 2);

			return ret_val;
		}

		public static float LerpValue(Vector3 start, Vector3 end, Vector3 current)
		{
			float ret_val = 0;

			float start_current_dist = Vector3.Distance(start, current);
			float end_current_dist = Vector3.Distance(end, current);

			float total = start_current_dist + end_current_dist;

			ret_val = start_current_dist / total;

			return ret_val;
		}

		public static Vector3 Average(List<Vector3> vectors, Action<object> output = null)
		{
			Vector3 average = new Vector3(0, 0, 0);

			if (vectors.Count < 1)
				return average;

			foreach (Vector3 vec in vectors)
				average += vec;

			average /= vectors.Count;

			return average;
		}

		public static Item LoadGun(Item gun)
		{
			BaseEntity gun_held_entity = gun.GetHeldEntity();

			if (gun_held_entity is BaseProjectile)
			{
				BaseProjectile projectile = (BaseProjectile)gun_held_entity;
				projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
			}

			return gun;
		}

		public static BaseHelicopter CreateHelicopter(Vector3 position, float volume = 1.0f)
		{
			string heli_prefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";

			BaseHelicopter heli = (BaseHelicopter)GameManager.server.CreateEntity(heli_prefab, new Vector3(), new Quaternion(), true);

			heli.GetComponent<PatrolHelicopterAI>();

			heli.transform.position = position;

			// TODO: idk, this changed in september
			heli.rotorWashSoundDef.volume = volume;
			//heli.engineSoundDef.volume = volume;
			//heli.rotorSoundDef.volume = volume;

			return heli;
		}

		public static BasePlayer CreatePlayer(Vector3 position, Vector3 look, string display_name = "Blaster :D")
		{
			string prefab = "assets/prefabs/player/player.prefab";

			BasePlayer player = (BasePlayer)GameManager.server.CreateEntity(prefab, position, Quaternion.LookRotation(look, Vector3.up));

			player._displayName = display_name;

			player.Spawn();

			player.SendNetworkUpdateImmediate();

			return player;
		}

		public static int GetAmmoStackSize(string ammo_type)
		{
			int stack_size = 0;

			switch (ammo_type)
			{
				case "ammo.rifle":
				case "ammo.pistol":
					stack_size = 128;
					break;
				case "ammo.shotgun":
					stack_size = 64;
					break;
				case "lowgradefuel":
					stack_size = 500;
					break;
				default:
					stack_size = 128;
					break;
			}

			return stack_size;
		}
	}
}
