using System;
using System.Collections.Generic;

namespace RustLib.DataStructures
{
	// binary heap
	// adapted from: 
	// https://miafish.wordpress.com/2015/03/16/c-min-heap-implementation/
	public class PriorityQueue<T>
	{
		private List<T> elements = new List<T>();
		private IComparer<T> comparator;

		public PriorityQueue(IComparer<T> comparator)
		{
			this.comparator = comparator;
		}

		public int Count => elements.Count;

		public void Enqueue(T item)
		{
			elements.Add(item);
			this.HeapifyUp(elements.Count - 1);
		}

		public T Dequeue()
		{
			if (elements.Count > 0)
			{
				T item = elements[0];
				elements[0] = elements[elements.Count - 1];
				elements.RemoveAt(elements.Count - 1);

				this.HeapifyDown(0);
				return item;
			}

			throw new InvalidOperationException("no element in heap");
		}

		private void HeapifyUp(int index)
		{
			int parent = this.GetParent(index);

			if (parent < 0) return;

			int comparison = comparator.Compare(elements[index], elements[parent]);

			if (parent >= 0 && comparison < 0)
			{
				T temp = elements[index];
				elements[index] = elements[parent];
				elements[parent] = temp;

				this.HeapifyUp(parent);
			}
		}

		private void HeapifyDown(int index)
		{
			int smallest = index;

			int left = this.GetLeft(index);
			int right = this.GetRight(index);
			int comparison;

			if (left < elements.Count)
			{
				comparison = comparator.Compare(elements[left], elements[index]);

				if (left < this.Count && comparison < 0)
					smallest = left;
			}

			if (right < elements.Count)
			{
				comparison = comparator.Compare(elements[right], elements[smallest]);

				if (right < this.Count && comparison < 0)
					smallest = right;
			}

			if (smallest != index)
			{
				T temp = elements[index];
				elements[index] = elements[smallest];
				elements[smallest] = temp;

				this.HeapifyDown(smallest);
			}
		}

		private int GetParent(int index)
		{
			if (index <= 0)
			{
				return -1;
			}

			return (index - 1) / 2;
		}

		private int GetLeft(int index)
		{
			return 2 * index + 1;
		}

		private int GetRight(int index)
		{
			return 2 * index + 2;
		}
	}
}
