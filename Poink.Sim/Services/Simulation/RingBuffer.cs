
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

    public void Add(T item)
    {
        int index = (_start + Count) % _items.Length;
        _items[index] = item;
        if (Count < _items.Length)
            Count++;
        else
            _start = (_start + 1) % _items.Length;
    }

    public void Clear()
    {
        _start = 0;
        Count = 0;
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

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
