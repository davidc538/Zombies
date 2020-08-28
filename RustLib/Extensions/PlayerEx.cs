namespace RustLib.Extensions
{
	public static class PlayerEx
	{
		public static void GiveWeapon(this BasePlayer input, int item_id)
		{
			Item item = ItemManager.CreateByItemID(item_id);
			input.GiveWeapon(item);
		}

		public static void GiveWeapon(this BasePlayer input, Item item)
		{
			input.inventory.GiveItem(item, input.inventory.containerBelt);
		}

		public static void GiveAttire(this BasePlayer input, int item_id)
		{
			Item item = ItemManager.CreateByItemID(item_id);
			input.GiveAttire(item);
		}

		public static void GiveAttire(this BasePlayer input, Item item)
		{
			input.inventory.GiveItem(item, input.inventory.containerWear);
		}

		public static void GiveItem(this BasePlayer input, int item_id)
		{
			Item item = ItemManager.CreateByItemID(item_id);
			input.GiveItem(item);
		}

		public static void GiveItem(this BasePlayer input, Item item)
		{
			input.inventory.GiveItem(item, input.inventory.containerMain);
		}

		public static void SwitchWeapon(this BasePlayer player, int belt_position)
		{
			uint new_weapon_uid = player.inventory.containerBelt.itemList[belt_position].uid;
			player.UpdateActiveItem(new_weapon_uid);
		}

		public static void SendDebugMessage(this BasePlayer player, string dbg)
		{
			player.ChatMessage(dbg);
			player.SendConsoleCommand($"echo {dbg}");
		}
	}
}