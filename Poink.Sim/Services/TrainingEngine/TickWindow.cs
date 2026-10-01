using System;
using System.Collections;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// A no-copy view over a slice of a tick list. The episode loop grows the view's end one tick at a
/// time so features only ever see ticks up to the current one, and feature math narrows the start to
/// the trailing window. Views over views are flattened onto the underlying list.
/// </summary>
internal sealed class TickWindow : IReadOnlyList<Tick>
{
	private readonly IReadOnlyList<Tick> _source;
	private readonly int _start;
	private int _end;

	public TickWindow(IReadOnlyList<Tick> source, int start, int? end = null)
	{
		var offset = 0;
		var limit = source.Count;
		if (source is TickWindow inner)
		{
			_source = inner._source;
			offset = inner._start;
			limit = inner._end;
		}
		else
		{
			_source = source;
		}

		_end = offset + Math.Clamp(end ?? limit - offset, 0, limit - offset);
		_start = Math.Min(offset + Math.Max(start, 0), _end);
	}

	public int Count => _end - _start;

	public Tick this[int index] => index >= 0 && index < Count ? _source[_start + index] : throw new ArgumentOutOfRangeException(nameof(index));

	public void ExtendTo(int count) => _end = Math.Min(_start + count, _source.Count);

	public IEnumerator<Tick> GetEnumerator()
	{
		for (var i = _start; i < _end; i++)
			yield return _source[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
