using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
using RustLib.Math3D;

namespace RustLib.FileSystem
{
	public static class Serializer
	{
		private class CustomSerializationBinder : SerializationBinder
		{
			public Action<object> debug = null;

			public override void BindToName(Type serialized_type, out string assembly_name, out string type_name)
			{
				assembly_name = "RustPlugin"; // this will be ignored
				type_name = serialized_type.Name;

				debug?.Invoke($"{serialized_type} => ASM: {assembly_name}, TYPE: {type_name}");
			}

			public override Type BindToType(string assembly_name, string type_name)
			{
				bool is_generic = type_name.Contains("[[");

				if (is_generic)
				{
					debug?.Invoke($"IsGeneric: {type_name}");

					int start = type_name.IndexOf("[[");
					int end = type_name.IndexOf("]]");

					string outer = type_name.Substring(0, start);
					string inner = type_name.Substring(start + 2, end - start - 2);

					debug?.Invoke($"Resolving generic type: {outer}<{inner}>");

					Type outer_type = Type.GetType(outer);
					Type inner_type = GetSimpleType(assembly_name, inner);

					if (outer_type == null)
						outer_type = Type.GetType(outer);

					debug?.Invoke($"Resolving generic type: {outer_type}<{inner_type}>");

					Type retval = outer_type.MakeGenericType(inner_type);

					return retval;
				}
				else
					return GetSimpleType(assembly_name, type_name);
			}

			private Type GetSimpleType(string assembly_name, string type_name)
			{
				Assembly[] loaded_assemblies = AppDomain.CurrentDomain.GetAssemblies();

				Type temp = null;

				debug?.Invoke($"GetSimpleType: {assembly_name}, {type_name}");

				foreach (Assembly assembly in loaded_assemblies)
				{
					Type temp2 = assembly.GetType(type_name);

					if (temp2 != null)
					{
						temp = temp2;
						debug?.Invoke($"{temp.Name}, {temp.Assembly.FullName}");
					}
				}

				return temp;
			}
		}

		public static void SerializeToDisk(string filename, object o, Action<object> debug = null)
		{
			string json = JsonConvert.SerializeObject(o, GetSerializerSettings(debug));

			int start_index = json.IndexOf(", plugins");
			int end_index = json.IndexOf('"', start_index);
			string remove_me = json.Substring(start_index, end_index - start_index);

			json = json.Replace(remove_me, "");

			File.WriteAllText(filename, json);
		}

		public static T DeserializeFromDisk<T>(string filename, Action<object> debug = null)
		{
			string[] lines = File.ReadAllLines(filename);

			StringBuilder sb = new StringBuilder();

			foreach (string line in lines)
				sb.Append(line);

			string json = sb.ToString();

			debug?.Invoke(json);

			T retval = JsonConvert.DeserializeObject<T>(json, GetSerializerSettings(debug));

			return retval;
		}

		private static JsonSerializerSettings GetSerializerSettings(Action<object> debug = null)
		{
			JsonSerializerSettings retval = new JsonSerializerSettings();
			CustomSerializationBinder binder = new CustomSerializationBinder();

			binder.debug = debug;
			retval.Binder = binder;
			retval.MaxDepth = int.MaxValue;
			retval.TypeNameHandling = TypeNameHandling.Auto;
			retval.Formatting = Formatting.Indented;
			retval.TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple;

			return retval;
		}

		public static string GetCurrentWorkingDirectory()
		{
			return AppDomain.CurrentDomain.RelativeSearchPath;
		}
	}

	public class Arena
	{
		public float time_of_day;
		public AABB aabb;

		[JsonIgnore]
		public string filename { get; private set; }

		public static Arena LoadByName(string name)
		{
			string dir = $"arenas\\{name}\\config";
			Arena arena = Interface.Oxide.DataFileSystem.ReadObject<Arena>(dir);
			arena.filename = dir;
			return arena;
		}
	}
}
