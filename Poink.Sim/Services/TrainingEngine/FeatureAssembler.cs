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
/// XGrid Signals isn't implemented yet -- it needs a tick-to-bar/histogram conversion this class
/// doesn't own (see AssembleXGridSignals); TxFold and Trader State are fully real.
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

	// TxFoldStats.Compute (Components/Charts/TxFold) is the shared source of truth the TxFold chart
	// itself renders from -- not part of the old BrainCatalog/IObservationEncoder training pipeline.
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
			return new TxFoldFeature(new Dictionary<int, float[]>());

		var snapshot = TxFoldStats.Compute(ticks, asOf, episodeStart, selectedSeconds, options.BarKind);
		var barsBySeconds = snapshot.Bars.ToDictionary(bar => bar.LookbackSeconds, bar => BarToValues(bar.Bar));

		var values = new Dictionary<int, float[]>();
		foreach (var seconds in selectedSeconds.Distinct())
			values[seconds] = barsBySeconds.TryGetValue(seconds, out var bar) ? bar : new float[TxFoldValuesPerTimeframe];
		return new TxFoldFeature(values);
	}

	private static float[] BarToValues(CTraderBarRow bar) => [(float)bar.Open, (float)bar.High, (float)bar.Low, (float)bar.Close];

	private static TraderStateFeature AssembleTraderState(TraderStateOptionsModel options, TraderStateSnapshot snapshot) =>
		new(
			options.IncludePosition ? snapshot.Position : null,
			options.IncludePositionPrice ? snapshot.PositionPrice : null,
			options.IncludePositionTicks ? snapshot.PositionTicks : null,
			options.IncludePositionDistance ? snapshot.PositionDistance : null,
			options.IncludeCurrentPrice ? snapshot.CurrentPrice : null,
			options.IncludeCash ? snapshot.Cash : null,
			options.IncludeRealized ? snapshot.Realized : null,
			options.IncludeUnrealized ? snapshot.Unrealized : null,
			options.IncludeEquity ? snapshot.Equity : null,
			options.IncludeMargin ? snapshot.Margin : null,
			options.IncludeMarginLevel ? snapshot.MarginLevel : null,
			options.IncludePositionCount ? snapshot.PositionCount : null,
			options.IncludeOutcomes ? PadOrTrim(snapshot.RecentOutcomes, options.OutcomeListLength) : null,
			options.IncludePositionLevel ? PadOrTrim(snapshot.RecentPositionLevels, options.PositionLevelListLength) : null,
			options.IncludeDirection ? PadOrTrim(snapshot.RecentDirections, options.DirectionListLength) : null
		);

	private static IReadOnlyList<double> PadOrTrim(IReadOnlyList<double> values, int length)
	{
		if (values.Count == length)
			return values;
		if (values.Count > length)
			return values.Take(length).ToList();

		var padded = new List<double>(values);
		padded.AddRange(Enumerable.Repeat(0.0, length - values.Count));
		return padded;
	}

	// Order is fixed (XGrid Signals, then TxFold, then Trader State; each sorted/positional within
	// itself) so the same FeatureOptionsModel always produces a vector Network Assembler can size.
	public FeatureVector Flatten(FeatureModel model)
	{
		var values = new List<float>();

		foreach (var (_, value) in model.XGridSignals.SignalValues.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			values.Add((float)value);

		foreach (var (_, bar) in model.TxFold.ValuesBySeconds.OrderBy(kv => kv.Key))
			values.AddRange(bar);

		values.AddRange(TraderStateValues(model.TraderState).Select(v => (float)v));

		return new FeatureVector([.. values]);
	}

	// The flattened vector's length for a given options set, without real tick data -- safe since
	// AssembleTxFold/AssembleTraderState always produce a fixed shape per selection.
	public static int ComputeInputSize(FeatureOptionsModel options)
	{
		var xGridCount = options.XGridSignals.EnabledSignals.Count;
		var txFoldCount = (options.TxFold.SelectedSeconds?.Count ?? 0) * TxFoldValuesPerTimeframe;
		return xGridCount + txFoldCount + TraderStateInputSize(options.TraderState);
	}

	private static int TraderStateInputSize(TraderStateOptionsModel options)
	{
		var scalarCount = new[]
		{
			options.IncludePosition,
			options.IncludePositionPrice,
			options.IncludePositionTicks,
			options.IncludePositionDistance,
			options.IncludeCurrentPrice,
			options.IncludeCash,
			options.IncludeRealized,
			options.IncludeUnrealized,
			options.IncludeEquity,
			options.IncludeMargin,
			options.IncludeMarginLevel,
			options.IncludePositionCount,
		}.Count(include => include);

		var listCount =
			(options.IncludeOutcomes ? options.OutcomeListLength : 0)
			+ (options.IncludePositionLevel ? options.PositionLevelListLength : 0)
			+ (options.IncludeDirection ? options.DirectionListLength : 0);

		return scalarCount + listCount;
	}

	private static IEnumerable<double> TraderStateValues(TraderStateFeature state)
	{
		foreach (
			var scalar in new[]
			{
				state.Position,
				state.PositionPrice,
				state.PositionTicks,
				state.PositionDistance,
				state.CurrentPrice,
				state.Cash,
				state.Realized,
				state.Unrealized,
				state.Equity,
				state.Margin,
				state.MarginLevel,
				state.PositionCount,
			}
		)
			if (scalar is { } value)
				yield return value;

		foreach (var list in new[] { state.Outcomes, state.PositionLevels, state.Directions })
			if (list is not null)
				foreach (var value in list)
					yield return value;
	}
}
