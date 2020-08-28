using Newtonsoft.Json;
using System.IO;

namespace RustLib.FileSystem
{
	public static class JSONFile
	{
		public static T LoadJSON<T>(string filename)
		{
			string dir = $"{Directory.GetCurrentDirectory()}\\oxide\\data\\{filename}.json";
			string json;
			using (StreamReader reader = new StreamReader(dir)) { json = reader.ReadToEnd(); }
			T ret = JsonConvert.DeserializeObject<T>(json);
			return ret;
		}

		public static void SaveJSON<T>(string filename, T data)
		{
			string dir = $"{Directory.GetCurrentDirectory()}\\oxide\\data\\{filename}.json";
			string json = JsonConvert.SerializeObject(data);

			using (StreamWriter writer = new StreamWriter(dir)) { writer.Write(json); }
		}

		/*
		public static T LoadJSON<T>(string filename)
		{
			 string dir = $"{Directory.GetCurrentDirectory()}\\oxide\\data\\{filename}.json";
			 string json = File.ReadAllText(dir);
			 T ret = JsonConvert.DeserializeObject<T>(json);
			 return ret;
		}

		public static void SaveJSON<T>(string filename, T data)
		{
			 string dir = $"{Directory.GetCurrentDirectory()}\\oxide\\data\\{filename}.json";
			 string json = JsonConvert.SerializeObject(data);
			 File.WriteAllText(dir, json);
		}
		//*/
	}
}
