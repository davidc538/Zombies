using RustLib.Math3D;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace RustLib.FileSystem
{
	public class EntitySpawner
	{
		public class SpawnPoint
		{
			public string entity_type;
			public Vec3 location;
			public Quat rotation;

			public SpawnPoint()
			{

			}

			public SpawnPoint(string entity_type, Vector3 location, Quaternion rotation)
			{
				this.entity_type = entity_type;
				this.location = new Vec3(location);
				this.rotation = new Quat(rotation);
			}

			public Vector3 Location() => location.Vector3();
			public Quaternion Rotation() => rotation.Quaternion();
		}

		[JsonIgnore]
		public string filename { get; private set; }

		public List<SpawnPoint> spawn_points = new List<SpawnPoint>();

		public static EntitySpawner LoadFile(string filename)
		{
			EntitySpawner ret = Interface.Oxide.DataFileSystem.ReadObject<EntitySpawner>(filename);
			ret.filename = filename;
			return ret;
		}

		public void Save(string filename)
		{
			Interface.Oxide.DataFileSystem.WriteObject(filename, this);
		}
	}

	public class Spawner
	{
		public List<SpawnPoint> spawn_points = new List<SpawnPoint>();

		[JsonIgnore]
		public string filename { get; private set; }

		public static Spawner LoadFile(string filename)
		{
			Spawner ret = Interface.Oxide.DataFileSystem.ReadObject<Spawner>(filename);
			ret.filename = filename;
			return ret;
		}

		public void Save(string filename)
		{
			Interface.Oxide.DataFileSystem.WriteObject(filename, this);
		}

		public void Add(Vector3 point, Quaternion rotation)
		{
			spawn_points.Add(new SpawnPoint(point, rotation));
		}

		public SpawnPoint GetSafeSpawnPoint_NaiveMethod(List<Vector3> points_to_avoid, System.Random random = null)
		{
			try
			{
				if (random == null)
					random = new System.Random();

				if (points_to_avoid.Count < 1)
					return GetRandomSpawnPoint(random);

				List<SpawnPoint> points = new List<SpawnPoint>();

				foreach (SpawnPoint sp in spawn_points)
					points.Add(sp);

				Dictionary<SpawnPoint, float> distances = MathUtils.AssignDistances(points, points_to_avoid);

				points = MathUtils.RankPointsSuitability(points, distances);

				int max_index = points.Count / 4;

				if (max_index < 3 && points.Count > 5)
					max_index = 3;

				int index = random.Next(max_index);

				SpawnPoint ret = points[index];

				return ret;
			}
			catch (System.Exception e)
			{
				throw new System.Exception($"Spawner with empty list?: {filename}", e);
			}
		}

		public SpawnPoint GetRandomSpawnPoint(System.Random random = null)
		{
			if (random == null)
				random = new System.Random();

			int random_index = random.Next(spawn_points.Count);
			SpawnPoint random_point = spawn_points[random_index];

			return random_point;
		}
	}
}
