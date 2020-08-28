using Newtonsoft.Json;
using Oxide.Core;
using RustLib.Math3D;
using System.Collections.Generic;
using UnityEngine;

namespace RustLib.FileSystem
{
	public class DoorCommandList
	{
		public class DoorCommand
		{
			public Vec3 location;
			public string command;

			public DoorCommand(Vec3 location, string command)
			{
				this.location = location;
				this.command = command;
			}
		}

		[JsonIgnore]
		public string filename { get; private set; }

		public List<DoorCommand> door_commands = new List<DoorCommand>();
		public float distance = 0.1f;

		public static DoorCommandList LoadFile(string filename)
		{
			DoorCommandList ret = Interface.Oxide.DataFileSystem.ReadObject<DoorCommandList>(filename);
			ret.filename = filename;
			return ret;
		}

		public void Save(string filename)
		{
			Interface.Oxide.DataFileSystem.WriteObject(filename, this);
		}

		public string FindCommand(Vector3 location)
		{
			DoorCommand cmd = FindDoorCommand(location);
			return cmd?.command;
		}

		private DoorCommand FindDoorCommand(Vector3 location)
		{
			DoorCommand door_command = null;

			foreach (DoorCommand command in door_commands)
			{
				float dist = Vector3.Distance(command.location.Vector3(), location);

				if (dist < distance)
					door_command = command;
			}

			return door_command;
		}

		public void SetCommand(Vector3 location, string command)
		{
			List<DoorCommand> to_remove = new List<DoorCommand>();

			foreach (DoorCommand cmd in door_commands)
			{
				float dist = Vector3.Distance(cmd.location.Vector3(), location);

				if (dist < distance)
					to_remove.Add(cmd);
			}

			foreach (DoorCommand cmd in to_remove)
				door_commands.Remove(cmd);

			door_commands.Add(new DoorCommand(new Vec3(location), command));
		}
	}
}
