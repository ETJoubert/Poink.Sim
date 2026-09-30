using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Turns FeatureOptionsModel's selections into a FeatureModel (structured, for display/inspection)
/// and a flattened FeatureVector (for training), from live tick data and a trader-state snapshot.
/// </summary>
internal sealed class FeatureAssembler
{
	private const int TxFoldValuesPerTimeframe = 4;

	public FeatureModel Assemble(
		FeatureOptionsModel options,
		IReadOnlyList<Tick> ticks,
		DateTime asOf,
		DateTime episodeStart,
		TraderStateSnapshot traderState
	) =>
		new(
			AssembleXGridSignals(options.XGridSignals),
			AssembleTxFold(options.TxFold, ticks, asOf, episodeStart),
			AssembleTraderState(options.TraderState, traderState)
		);

	private static XGridSignalsFeature AssembleXGridSignals(XGridSignalsOptionsModel options)
	{
		if (options.EnabledSignals.Count == 0)
			return new XGridSignalsFeature(new Dictionary<string, double>());

		throw new NotSupportedException(
			"XGrid Signals encoding needs a tick-to-bar/histogram conversion step that isn't built yet -- disable XGrid Signals until it is."
		);
	}

	// Every selected timeframe always gets an entry, zeroed when there's too little history --
	// otherwise the vector's length would shrink early in an episode, breaking the network's fixed input size.
	private static TxFoldFeature AssembleTxFold(TxFoldOptionsModel options, IReadOnlyList<Tick> ticks, DateTime asOf, DateTime episodeStart)
	{
		var selectedSeconds = options.SelectedSeconds ?? [];
		if (selectedSeconds.Count == 0)
			return new TxFoldFeature(new Dictionary<int, float[]>(), [], null, new Dictionary<TxFoldLevelKind, float>());

		var snapshot = TxFoldStats.Compute(ticks, asOf, episodeStart, selectedSeconds, options.BarKind);
		var valuesPerTimeframe = ValuesPerTimeframe(options);

		double? referencePrice = null;
		double? referenceRange = null;
		if (options.Normalize && snapshot.Bars.Count > 0)
		{
			var referenceBar = snapshot.Bars.OrderByDescending(b => b.LookbackSeconds).First();
			referencePrice = (referenceBar.TrueHigh + referenceBar.TrueLow) / 2;
			referenceRange = (referenceBar.TrueHigh - referenceBar.TrueLow) / 2;
		}

		var barsBySeconds = snapshot.Bars.ToDictionary(
			bar => bar.LookbackSeconds,
			bar => BarToValues(bar, options.IncludeHighLow, referencePrice, referenceRange)
		);

		var values = new Dictionary<int, float[]>();
		foreach (var seconds in selectedSeconds.Distinct())
			values[seconds] = barsBySeconds.TryGetValue(seconds, out var bar) ? bar : new float[valuesPerTimeframe];

		var volumeProfile = options.IncludeVolumeProfile
			? TxFoldStats.BuildVolumeProfileFeature(ticks, options.VolumeProfileBucketCount)
			: [];

		float? currentPrice = null;
		if (options.IncludeCurrentPrice)
		{
			var lastMid = ticks.Count > 0 ? (ticks[^1].Bid + ticks[^1].Ask) / 2.0 : 0.0;
			currentPrice = (float)Normalize(lastMid, referencePrice, referenceRange);
		}

		var levels = new Dictionary<TxFoldLevelKind, float>();
		foreach (var kind in options.IncludedLevels ?? [])
			levels[kind] = (float)Normalize(TxFoldStats.LevelValue(snapshot.Levels, kind) ?? 0, referencePrice, referenceRange);

		return new TxFoldFeature(values, volumeProfile, currentPrice, levels);
	}

	private static int ValuesPerTimeframe(TxFoldOptionsModel options) => TxFoldValuesPerTimeframe + (options.IncludeHighLow ? 2 : 0);

	private static double Normalize(double value, double? referencePrice, double? referenceRange) =>
		referencePrice is { } price && referenceRange is > 0 ? (value - price) / referenceRange.Value : value;

	private static float[] BarToValues(TxFoldBar bar, bool includeHighLow, double? referencePrice, double? referenceRange)
	{
		float N(double value) => (float)Normalize(value, referencePrice, referenceRange);

		if (!includeHighLow)
			return [N(bar.Bar.Open), N(bar.Bar.High), N(bar.Bar.Low), N(bar.Bar.Close)];

		return [N(bar.Bar.Open), N(bar.Bar.High), N(bar.Bar.Low), N(bar.Bar.Close), N(bar.TrueHigh), N(bar.TrueLow)];
	}

	private static TraderStateFeature AssembleTraderState(TraderStateOptionsModel options, TraderStateSnapshot snapshot) =>
		new(options.IncludeUnrealized ? snapshot.Unrealized : null);

	// Order is fixed (XGrid Signals, then TxFold, then Trader State; each sorted/positional within
	// itself) so the same FeatureOptionsModel always produces a vector Network Assembler can size.
	public FeatureVector Flatten(FeatureModel model)
	{
		var values = new List<float>();

		foreach (var (_, value) in model.XGridSignals.SignalValues.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			values.Add((float)value);

		foreach (var (_, bar) in model.TxFold.ValuesBySeconds.OrderBy(kv => kv.Key))
			values.AddRange(bar);
		values.AddRange(model.TxFold.VolumeProfile);
		if (model.TxFold.CurrentPrice is { } currentPrice)
			values.Add(currentPrice);
		values.AddRange(model.TxFold.Levels.OrderBy(kv => kv.Key).Select(kv => kv.Value));

		values.AddRange(TraderStateValues(model.TraderState).Select(v => (float)v));

		return new FeatureVector([.. values]);
	}

	// The flattened vector's length for a given options set, without real tick data -- safe since
	// AssembleTxFold/AssembleTraderState always produce a fixed shape per selection.
	public static int ComputeInputSize(FeatureOptionsModel options)
	{
		var xGridCount = options.XGridSignals.EnabledSignals.Count;
		var selectedCount = options.TxFold.SelectedSeconds?.Count ?? 0;
		var txFoldCount = selectedCount * ValuesPerTimeframe(options.TxFold);
		var volumeProfileCount = options.TxFold.IncludeVolumeProfile ? options.TxFold.VolumeProfileBucketCount : 0;
		var currentPriceCount = selectedCount > 0 && options.TxFold.IncludeCurrentPrice ? 1 : 0;
		var levelsCount = selectedCount > 0 ? options.TxFold.IncludedLevels?.Count ?? 0 : 0;
		return xGridCount + txFoldCount + volumeProfileCount + currentPriceCount + levelsCount + TraderStateInputSize(options.TraderState);
	}

	private static int TraderStateInputSize(TraderStateOptionsModel options) => options.IncludeUnrealized ? 1 : 0;

	private static IEnumerable<double> TraderStateValues(TraderStateFeature state)
	{
		if (state.Unrealized is { } value)
			yield return value;
	}
}
