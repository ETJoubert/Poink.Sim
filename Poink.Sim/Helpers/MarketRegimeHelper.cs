using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Helpers;

/// <summary>Volatility, trend and order-flow measures over a run of bars, oldest first.</summary>
internal static class MarketRegimeHelper
{
	/// <summary>Simple moving average of true range, one value per bar from the first full period on.</summary>
	public static List<double> AtrSeries(IReadOnlyList<HlocAvdtBar> bars, int period)
	{
		var result = new List<double>();
		if (period < 1 || bars.Count < period)
			return result;

		var trueRanges = new double[bars.Count];
		for (int i = 0; i < bars.Count; i++)
		{
			var bar = bars[i];
			trueRanges[i] =
				i == 0
					? bar.High - bar.Low
					: Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - bars[i - 1].Close), Math.Abs(bar.Low - bars[i - 1].Close)));
		}

		double sum = trueRanges.Take(period).Sum();
		result.Add(sum / period);
		for (int i = period; i < trueRanges.Length; i++)
		{
			sum += trueRanges[i] - trueRanges[i - period];
			result.Add(sum / period);
		}
		return result;
	}

	/// <summary>Where the latest ATR ranks among all ATRs in the series, 0-100; null without at least two values.</summary>
	public static double? Percentile(IReadOnlyList<double> series)
	{
		if (series.Count < 2)
			return null;
		double latest = series[^1];
		return 100.0 * series.Count(value => value <= latest) / series.Count;
	}

	/// <summary>Kaufman's efficiency ratio over the last <paramref name="period"/> bar closes: net move over the sum of moves, 0-1.</summary>
	public static double? EfficiencyRatio(IReadOnlyList<HlocAvdtBar> bars, int period)
	{
		if (period < 1 || bars.Count < period + 1)
			return null;

		int start = bars.Count - period - 1;
		double path = 0;
		for (int i = start + 1; i < bars.Count; i++)
			path += Math.Abs(bars[i].Close - bars[i - 1].Close);
		return path > 0 ? Math.Abs(bars[^1].Close - bars[start].Close) / path : null;
	}

	/// <summary>Least-squares line through the bar closes: its rise per bar and how well it fits (R², 0-1).</summary>
	public static (double SlopePerBar, double RSquared)? Regression(IReadOnlyList<HlocAvdtBar> bars)
	{
		int n = bars.Count;
		if (n < 3)
			return null;

		double meanX = (n - 1) / 2.0;
		double meanY = bars.Average(bar => bar.Close);
		double sxy = 0,
			sxx = 0,
			syy = 0;
		for (int i = 0; i < n; i++)
		{
			double dx = i - meanX,
				dy = bars[i].Close - meanY;
			sxy += dx * dy;
			sxx += dx * dx;
			syy += dy * dy;
		}
		if (sxx == 0 || syy == 0)
			return (0, 0);
		return (sxy / sxx, sxy * sxy / (sxx * syy));
	}

	/// <summary>Up-tick minus down-tick volume as a share of both, -1 (all selling) to 1 (all buying); null with no directional ticks.</summary>
	public static double? VolumeImbalance(IReadOnlyList<HlocAvdtBar> bars)
	{
		long up = bars.Sum(bar => (long)bar.UpVolume);
		long down = bars.Sum(bar => (long)bar.DownVolume);
		return up + down > 0 ? (double)(up - down) / (up + down) : null;
	}
}
