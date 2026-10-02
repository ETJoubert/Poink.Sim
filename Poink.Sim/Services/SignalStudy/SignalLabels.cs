using System;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>
/// What followed each point of a series: the mid-price move at every horizon, in ATRs, and the result of a stop/target
/// trade opened there on each side, in R. Fills use the bid and ask, so the spread is paid; NaN marks a point whose
/// outcome the data cannot show (no ATR yet, or the data ends first).
/// </summary>
internal sealed class SignalLabels
{
	// A forward point this long past its horizon means a market close sat in between, so the move is not a horizon move.
	private static readonly TimeSpan MaxHorizonOvershoot = TimeSpan.FromMinutes(5);

	private SignalLabels(int horizons, int count)
	{
		ForwardAtr = new double[horizons][];
		for (int h = 0; h < horizons; h++)
			ForwardAtr[h] = new double[count];
		LongR = new double[count];
		ShortR = new double[count];
		LongWin = new bool[count];
		ShortWin = new bool[count];
	}

	public double[][] ForwardAtr { get; }
	public double[] LongR { get; }
	public double[] ShortR { get; }
	public bool[] LongWin { get; }
	public bool[] ShortWin { get; }

	public static SignalLabels Compute(SignalPointSeries series, SignalAnalysisOptions options, CancellationToken token)
	{
		var labels = new SignalLabels(options.HorizonMinutes.Count, series.Count);
		for (int h = 0; h < options.HorizonMinutes.Count; h++)
			FillForward(series, TimeSpan.FromMinutes(options.HorizonMinutes[h]), labels.ForwardAtr[h]);

		long maxTicks = TimeSpan.FromMinutes(options.MaxMinutes).Ticks;
		Parallel.For(
			0,
			series.Count,
			new ParallelOptions { CancellationToken = token },
			i => FillTrade(series, options, maxTicks, i, labels)
		);
		return labels;
	}

	private static void FillForward(SignalPointSeries series, TimeSpan horizon, double[] forward)
	{
		long horizonTicks = horizon.Ticks;
		long overshootTicks = MaxHorizonOvershoot.Ticks;
		int j = 0;
		for (int i = 0; i < series.Count; i++)
		{
			long target = series.Ticks[i] + horizonTicks;
			if (j <= i)
				j = i + 1;
			while (j < series.Count && series.Ticks[j] < target)
				j++;

			double atr = series.Atr[i];
			forward[i] =
				j < series.Count && series.Ticks[j] - target <= overshootTicks && atr > 0
					? (series.Mid(j) - series.Mid(i)) / atr
					: double.NaN;
		}
	}

	// A stop fills at the price that crossed it, so a gap through it costs more than 1R; a target fills at the target.
	private static void FillTrade(SignalPointSeries series, SignalAnalysisOptions options, long maxTicks, int i, SignalLabels labels)
	{
		double atr = series.Atr[i];
		labels.LongR[i] = double.NaN;
		labels.ShortR[i] = double.NaN;
		if (!(atr > 0))
			return;

		double risk = options.StopAtr * atr;
		double longEntry = series.Ask[i];
		double shortEntry = series.Bid[i];
		double longStop = longEntry - risk,
			longTarget = longEntry + options.TargetR * risk;
		double shortStop = shortEntry + risk,
			shortTarget = shortEntry - options.TargetR * risk;
		long deadline = series.Ticks[i] + maxTicks;

		double? longR = null,
			shortR = null;
		int last = -1;
		for (int j = i + 1; j < series.Count && series.Ticks[j] <= deadline && (longR is null || shortR is null); j++)
		{
			last = j;
			if (longR is null)
			{
				if (series.Bid[j] <= longStop)
					longR = (series.Bid[j] - longEntry) / risk;
				else if (series.Bid[j] >= longTarget)
				{
					longR = options.TargetR;
					labels.LongWin[i] = true;
				}
			}
			if (shortR is null)
			{
				if (series.Ask[j] >= shortStop)
					shortR = (shortEntry - series.Ask[j]) / risk;
				else if (series.Ask[j] <= shortTarget)
				{
					shortR = options.TargetR;
					labels.ShortWin[i] = true;
				}
			}
		}

		bool windowCovered = last >= 0 && (last + 1 < series.Count || series.Ticks[last] >= deadline);
		if (longR is null && windowCovered)
			longR = (series.Bid[last] - longEntry) / risk;
		if (shortR is null && windowCovered)
			shortR = (shortEntry - series.Ask[last]) / risk;
		labels.LongR[i] = longR ?? double.NaN;
		labels.ShortR[i] = shortR ?? double.NaN;
	}
}
