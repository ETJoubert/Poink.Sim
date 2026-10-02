using System;
using System.Collections.Generic;
using Poink.Sim.Helpers;

namespace Poink.Sim.Models
{
	/// <summary>
	/// How a signal dataset is labelled and ranked: the forward-return horizons, the stop/target/time-limit trade tested
	/// from each entry, the fewest entries a state needs to be ranked, and how many top states are combined into pairs.
	/// </summary>
	public sealed record SignalAnalysisOptions
	{
		public IReadOnlyList<int> HorizonMinutes { get; init; } = new[] { 5, 15, 60 };

		/// <summary>Stop distance of the tested trade, in ATRs; its R.</summary>
		public double StopAtr { get; init; } = 1.5;

		/// <summary>Target distance of the tested trade, in multiples of the stop distance.</summary>
		public double TargetR { get; init; } = 1.5;

		/// <summary>Minutes before the tested trade is closed at the market if neither stop nor target was hit.</summary>
		public int MaxMinutes { get; init; } = 60;

		public int MinEntries { get; init; } = 30;

		/// <summary>Whether an entry is ignored while the previous one of the same state would still be an open trade, so samples do not overlap.</summary>
		public bool NonOverlapping { get; init; } = true;

		/// <summary>How many of the best single states are combined two at a time.</summary>
		public int PairCandidates { get; init; } = 12;
	}

	public enum SignalTradeSide
	{
		Long,
		Short,
	}

	/// <summary>
	/// What followed the entries into one signal state (or pair of states): forward moves per horizon, the tested trade's
	/// result on each side, and whether the first and second halves of the range agree.
	/// </summary>
	public sealed record SignalStateStats(
		string Name,
		string Key,
		RuleSignalGroup? Group,
		int Entries,
		IReadOnlyList<double?> MeanForwardAtr,
		IReadOnlyList<double?> ForwardCiAtr,
		IReadOnlyList<double?> LiftAtr,
		double? LongWinRate,
		double? LongExpectancyR,
		double? ShortWinRate,
		double? ShortExpectancyR,
		SignalTradeSide BestSide,
		double? BestExpectancyR,
		double? BestTStat,
		double? FirstHalfR,
		double? SecondHalfR,
		double? MeanSpreadPips
	)
	{
		public bool HalvesAgree => FirstHalfR is { } first && SecondHalfR is { } second && Math.Sign(first) == Math.Sign(second) && first != 0;
	}

	public sealed record SignalAnalysisResult(
		long DatasetId,
		string DatasetName,
		SignalAnalysisOptions Options,
		long PointCount,
		SignalStateStats Baseline,
		IReadOnlyList<SignalStateStats> Singles,
		IReadOnlyList<SignalStateStats> Pairs,
		TimeSpan Elapsed
	);
}
