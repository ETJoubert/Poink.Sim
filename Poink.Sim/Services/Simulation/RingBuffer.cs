// ── Fixed-capacity circular buffer, most-recent-last ─────────────────────
//  Drop-in IReadOnlyList<T> replacement for the "List<T>, RemoveAt(0) once
//  over capacity" pattern SimulationEngine used for its recent-ticks/bars/log
//  buffers. RemoveAt(0) on a List shifts every remaining element -- O(capacity)
//  -- so once the buffer filled up, every single Append paid for a full-buffer
//  shift. Add() here is O(1) instead: once full it just overwrites the oldest
//  slot and advances the start pointer.
// ─────────────────────────────────────────────────────────────────────────
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
