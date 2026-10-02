using System;
using System.Collections;
using System.Collections.Generic;

namespace Poink.Sim.Simulation;

internal sealed class RingBuffer<T> : IReadOnlyList<T>
{
	private readonly T[] _items;
	private int _start;

	public RingBuffer(int capacity) => _items = new T[capacity];

	public int Count { get; private set; }

	/// <summary>Changes on every Add and Clear, so a caller can tell whether a copy it took is still current.</summary>
	public long Version { get; private set; }

	public void Add(T item)
	{
		int index = (_start + Count) % _items.Length;
		_items[index] = item;
		if (Count < _items.Length)
			Count++;
		else
			_start = (_start + 1) % _items.Length;
		Version++;
	}

	public void Clear()
	{
		_start = 0;
		Count = 0;
		Version++;
	}

	public T this[int index]
	{
		get
		{
			if ((uint)index >= (uint)Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			return _items[(_start + index) % _items.Length];
		}
	}

	/// <summary>Copies the items oldest first, leaving room for <paramref name="extraCapacity"/> more without regrowing.</summary>
	public List<T> ToList(int extraCapacity = 0)
	{
		var list = new List<T>(Count + extraCapacity);
		int headLength = Math.Min(Count, _items.Length - _start);
		list.AddRange(new ArraySegment<T>(_items, _start, headLength));
		list.AddRange(new ArraySegment<T>(_items, 0, Count - headLength));
		return list;
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < Count; i++)
			yield return this[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
