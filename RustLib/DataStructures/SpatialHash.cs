using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustLib.DataStructures
{
	public class SpatialHash<T>
	{
		public delegate T1 Validate<out T1, in T2>(T2 arg);

		protected struct Storage
		{
			public Vector3 location;
			public T obj;

			public Storage(Vector3 location, T obj)
			{
				this.location = location;
				this.obj = obj;
			}
		}

		public Vector3 cell_size { get; private set; }
		protected Dictionary<Vector3, List<Storage>> hash_table = new Dictionary<Vector3, List<Storage>>();

		public SpatialHash(Vector3 cell_size)
		{
			this.cell_size = cell_size;
		}

		public void Add(Vector3 point, T obj)
		{
			Vector3 rounded = RoundPoint(point, cell_size);

			if (!hash_table.ContainsKey(rounded))
				hash_table[rounded] = new List<Storage>();

			hash_table[rounded].Add(new Storage(point, obj));
		}

		public void Clear() => hash_table.Clear();

		public T GetNearestObject(Vector3 point, float max_distance = float.MaxValue, Validate<bool, T> validate = null)
		{
			float distance = cell_size.magnitude;

			bool has_found_objects = false;

			while (!has_found_objects)
			{
				has_found_objects = AreObjectsNear(point, distance);

				distance *= 2.0f;

				if (distance >= max_distance && !has_found_objects)
					return default(T);
			}

			List<Storage> storages_near = GetObjectsInNearestCells(point, distance, validate);

			Storage nearest_storage = storages_near[0];
			float nearest_distance = float.MaxValue;

			foreach (Storage storage in storages_near)
			{
				float current_distance = Vector3.Distance(storage.location, point);
				bool is_closer = (current_distance < nearest_distance);

				if (!is_closer) continue;

				nearest_distance = current_distance;
				nearest_storage = storage;
			}

			return nearest_storage.obj;
		}

		public bool AreObjectsNear(Vector3 point, float distance, Validate<bool, T> validate = null)
		{
			List<T> objs = GetObjectsNear(point, distance, validate);

			return (objs.Count > 0);
		}

		public List<T> GetObjectsNear(Vector3 point, float distance, Validate<bool, T> validate = null)
		{
			List<T> ret_val = new List<T>();

			List<Storage> storage_objects = GetObjectsInNearestCells(point, distance, validate);

			foreach (Storage storage in storage_objects)
				ret_val.Add(storage.obj);

			return ret_val;
		}

		protected List<Storage> GetObjectsInNearestCells(Vector3 point, float distance, Validate<bool, T> validate = null)
		{
			List<Storage> ret_val = new List<Storage>();

			int cells_x = CellCount(cell_size.x, distance);
			int cells_y = CellCount(cell_size.y, distance);
			int cells_z = CellCount(cell_size.z, distance);

			List<Vector3> cells = GetNearestCells(point, cell_size, cells_x, cells_y, cells_z);

			foreach (Vector3 cell in cells)
			{
				List<Storage> items = NarrowPhase(point, cell, distance, validate);

				ret_val.AddRange(items);
			}

			return ret_val;
		}

		protected List<Vector3> GetNearestCells(Vector3 input, Vector3 multiple, int cells_x, int cells_y, int cells_z)
		{
			List<Vector3> ret_val = new List<Vector3>();

			int i, j, k;

			input = RoundPoint(input, multiple);
			Vector3 temp;

			for (i = -cells_x; i <= cells_x; i++)
			{
				for (j = -cells_y; j <= cells_y; j++)
				{
					for (k = -cells_z; k <= cells_z; k++)
					{
						temp.x = input.x + (multiple.x * i);
						temp.y = input.y + (multiple.y * j);
						temp.z = input.z + (multiple.z * k);

						ret_val.Add(temp);
					}
				}
			}

			return ret_val;
		}

		protected List<Storage> NarrowPhase(Vector3 point, Vector3 cell, float distance, Validate<bool, T> validate = null)
		{
			List<Storage> ret_val = new List<Storage>();

			if (!hash_table.ContainsKey(cell))
				return ret_val;

			foreach (Storage storage in hash_table[cell])
			{
				bool is_valid = (validate == null || validate(storage.obj));
				bool is_close_enough = (Vector3.Distance(storage.location, point) <= distance);

				if (is_valid && is_close_enough)
					ret_val.Add(storage);
			}

			return ret_val;
		}

		static protected Vector3 RoundPoint(Vector3 input, Vector3 multiple)
		{
			input.x = RoundUp(input.x, multiple.x);
			input.y = RoundUp(input.y, multiple.y);
			input.z = RoundUp(input.z, multiple.z);

			return input;
		}

		static protected float RoundUp(float input, float multiple)
		{
			if (multiple == 0.0f)
				return input;

			float remainder = Math.Abs(input) % multiple;

			if (remainder == 0.0f)
				return input;

			if (input < 0.0f)
				return -(Math.Abs(input) - remainder);
			else
				return input + multiple - remainder;
		}

		protected int CellCount(float cell_size, float distance)
		{
			return ((int)(distance / cell_size) + 1);
		}
	}
}
