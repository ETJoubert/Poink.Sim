using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>A signal dataset's points held as flat arrays, oldest first, for fast repeated analysis.</summary>
internal sealed class SignalPointSeries
{
	private SignalPointSeries(int count)
	{
		Count = count;
		Ticks = new long[count];
		Bid = new double[count];
		Ask = new double[count];
		Atr = new double[count];
		Above = new long[count];
		In = new long[count];
		Below = new long[count];
	}

	public int Count { get; }
	public long[] Ticks { get; }
	public double[] Bid { get; }
	public double[] Ask { get; }

	/// <summary>ATR at each point; NaN where it was not yet known.</summary>
	public double[] Atr { get; }

	public long[] Above { get; }
	public long[] In { get; }
	public long[] Below { get; }

	public double Mid(int index) => (Bid[index] + Ask[index]) / 2.0;

	public static SignalPointSeries From(IEnumerable<SignalPoint> points)
	{
		var buffered = new List<SignalPoint>(points);
		var series = new SignalPointSeries(buffered.Count);
		for (int i = 0; i < buffered.Count; i++)
		{
			var point = buffered[i];
			series.Ticks[i] = point.TimestampUtc.Ticks;
			series.Bid[i] = point.Bid;
			series.Ask[i] = point.Ask;
			series.Atr[i] = point.Atr is > 0 ? point.Atr.Value : double.NaN;
			series.Above[i] = point.AboveMask;
			series.In[i] = point.InMask;
			series.Below[i] = point.BelowMask;
		}
		return series;
	}

	/// <summary>The mask holding one of a catalog row's states: Above, In or Below.</summary>
	public long[] MaskFor(SignalStateSlot slot) =>
		slot switch
		{
			SignalStateSlot.Above => Above,
			SignalStateSlot.In => In,
			SignalStateSlot.Below => Below,
			_ => throw new ArgumentOutOfRangeException(nameof(slot)),
		};
}

internal enum SignalStateSlot
{
	Above,
	In,
	Below,
}
