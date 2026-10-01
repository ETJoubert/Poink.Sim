using System;
using System.Collections;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>A no-copy view of a tick list from a start index to its current end, so feature math sees only the trailing window.</summary>
internal sealed class TickWindow : IReadOnlyList<Tick>
{
	private readonly IReadOnlyList<Tick> _source;
	private readonly int _start;

	public TickWindow(IReadOnlyList<Tick> source, int start)
	{
		_source = source;
		_start = Math.Clamp(start, 0, source.Count);
	}

	public int Count => _source.Count - _start;

	public Tick this[int index] => index >= 0 && index < Count ? _source[_start + index] : throw new ArgumentOutOfRangeException(nameof(index));

	public IEnumerator<Tick> GetEnumerator()
	{
		for (var i = _start; i < _source.Count; i++)
			yield return _source[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
