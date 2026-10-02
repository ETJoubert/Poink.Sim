using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Helpers;

/// <summary>The market a rule set is evaluated against, beyond the baseline levels: completed bars, quote, clock and spread.</summary>
internal sealed record RuleSignalMarket(
	IReadOnlyList<HlocAvdtBar> CompletedBars,
	int Window,
	double? Bid,
	double? Ask,
	double? TypicalSpread,
	DateTime? TimestampUtc,
	TimeSpan BarSize,
	RuleSignalSettings Settings
);

/// <summary>The baseline levels the event and zone signals are measured against, at the in-band tolerance.</summary>
internal readonly record struct RuleSignalLevels(double? Poc, double? Vah, double? Val, double? Vwap, double CurrentPrice, double Tolerance);

/// <summary>
/// Builds the event, zone, regime, session and position signal rows. Every row keeps the Above/In/Below shape, so
/// masks, Need-N matching and Copy Live work on them unchanged.
/// </summary>
internal static class RuleSignalRowsHelper
{
	public const string EventsPrefix = "evt-";
	public const string ZonePrefix = "zone-";
	public const string RegimePrefix = "regime-";
	public const string SessionPrefix = "session-";
	public const string PositionPrefix = "pos-";

	public static IEnumerable<RuleSignalRow> MarketRows(
		RuleSignalLevels levels,
		RuleSignalMarket market,
		HashSet<RuleSignalGroup> disabledGroups
	)
	{
		var bars = market.CompletedBars;
		var settings = market.Settings;
		var atrSeries = MarketRegimeHelper.AtrSeries(bars, settings.AtrPeriod);
		double? atr = atrSeries.Count > 0 ? atrSeries[^1] : null;
		var windowBars = bars.Skip(Math.Max(0, bars.Count - Math.Max(1, market.Window))).ToList();

		var rows = new List<RuleSignalRow>();
		if (!disabledGroups.Contains(RuleSignalGroup.Events))
			rows.AddRange(EventRows(levels, bars, settings));
		if (!disabledGroups.Contains(RuleSignalGroup.Zone))
			rows.AddRange(ZoneRows(levels, bars, atr, settings));
		if (!disabledGroups.Contains(RuleSignalGroup.Regime))
			rows.AddRange(RegimeRows(bars, windowBars, atr, atrSeries, settings));
		if (!disabledGroups.Contains(RuleSignalGroup.Session))
			rows.AddRange(SessionRows(market, settings));
		return rows;
	}

	private static IEnumerable<RuleSignalRow> EventRows(RuleSignalLevels levels, IReadOnlyList<HlocAvdtBar> bars, RuleSignalSettings settings)
	{
		var closes = Last(bars, settings.EventLookbackBars).Select(bar => bar.Close).ToList();
		var named = new (string Name, string Key, double? Level)[]
		{
			("POC", "poc", levels.Poc),
			("VAH", "vah", levels.Vah),
			("VAL", "val", levels.Val),
			("VWAP", "vwap", levels.Vwap),
		};

		foreach (var (name, key, level) in named)
			yield return CrossRow(name, key, level, closes, levels, settings.EventLookbackBars);
		foreach (var (name, key, level) in named)
			yield return ReclaimRow(name, key, level, closes, levels, settings.EventLookbackBars);

		yield return FirstTouchRow("VAH", "vah", levels.Vah, bars, levels, settings.FirstTouchLookbackBars);
		yield return FirstTouchRow("VAL", "val", levels.Val, bars, levels, settings.FirstTouchLookbackBars);
	}

	private static RuleSignalRow CrossRow(
		string name,
		string key,
		double? level,
		IReadOnlyList<double> closes,
		RuleSignalLevels levels,
		int lookback
	)
	{
		bool up = false,
			down = false;
		if (level is { } l)
		{
			up = levels.CurrentPrice > l + levels.Tolerance && closes.Any(c => c < l - levels.Tolerance);
			down = levels.CurrentPrice < l - levels.Tolerance && closes.Any(c => c > l + levels.Tolerance);
		}
		return Tri(
			$"CROSS {name}",
			"--color-primary",
			$"{EventsPrefix}cross-{key}",
			up,
			level is not null && !up && !down,
			down,
			RuleSignalGroup.Events,
			$"Above: price is above {name} now after a bar close below it in the last {lookback} bars. Below: the reverse. In: no cross either way"
		);
	}

	private static RuleSignalRow ReclaimRow(
		string name,
		string key,
		double? level,
		IReadOnlyList<double> closes,
		RuleSignalLevels levels,
		int lookback
	)
	{
		bool reclaimed = false,
			lost = false;
		if (level is { } l)
		{
			bool Above(double c) => c > l + levels.Tolerance;
			bool Below(double c) => c < l - levels.Tolerance;
			reclaimed = levels.CurrentPrice > l + levels.Tolerance && HasSequence(closes, Above, Below);
			lost = levels.CurrentPrice < l - levels.Tolerance && HasSequence(closes, Below, Above);
		}
		return Tri(
			$"RECLAIM {name}",
			"--color-tertiary",
			$"{EventsPrefix}reclaim-{key}",
			reclaimed,
			level is not null && !reclaimed && !lost,
			lost,
			RuleSignalGroup.Events,
			$"Above: within the last {lookback} bars price closed above {name}, broke below it, and is back above (a failed breakdown). Below: the reverse (a failed breakout). In: neither"
		);
	}

	private static RuleSignalRow FirstTouchRow(
		string name,
		string key,
		double? level,
		IReadOnlyList<HlocAvdtBar> bars,
		RuleSignalLevels levels,
		int lookback
	)
	{
		bool firstTouch =
			level is { } l
			&& Math.Abs(levels.CurrentPrice - l) <= levels.Tolerance
			&& !Last(bars, lookback).Any(bar => bar.Low - levels.Tolerance <= l && l <= bar.High + levels.Tolerance);
		return InOnly(
			$"1ST TOUCH {name}",
			"--color-highlight",
			$"{EventsPrefix}touch-{key}-in",
			firstTouch,
			RuleSignalGroup.Events,
			$"Price is at {name} now and none of the previous {lookback} bars touched it"
		);
	}

	private static IEnumerable<RuleSignalRow> ZoneRows(
		RuleSignalLevels levels,
		IReadOnlyList<HlocAvdtBar> bars,
		double? atr,
		RuleSignalSettings settings
	)
	{
		double price = levels.CurrentPrice;
		bool hasArea = levels.Vah is not null && levels.Val is not null;
		double vah = levels.Vah ?? 0,
			val = levels.Val ?? 0;
		bool inside = hasArea && price >= val && price <= vah;

		yield return Tri(
			"VALUE AREA",
			"--color-highlight",
			$"{ZonePrefix}va",
			hasArea && price > vah,
			inside,
			hasArea && price < val,
			RuleSignalGroup.Zone,
			"Above: price is above VAH. In: inside the value area (VAL to VAH). Below: below VAL"
		);

		yield return AtrDistanceRow("VWAP", "vwap", levels.Vwap, price, atr, settings.AtrDistanceMultiple);
		yield return AtrDistanceRow("POC", "poc", levels.Poc, price, atr, settings.AtrDistanceMultiple);

		var recent = Last(bars, settings.EventLookbackBars).ToList();
		int lastAbove = hasArea ? recent.FindLastIndex(bar => bar.Close > vah) : -1;
		int lastBelow = hasArea ? recent.FindLastIndex(bar => bar.Close < val) : -1;
		bool fromAbove = inside && lastAbove >= 0 && lastAbove > lastBelow;
		bool fromBelow = inside && lastBelow >= 0 && lastBelow > lastAbove;
		yield return Tri(
			"VA RE-ENTRY",
			"--color-secondary",
			$"{ZonePrefix}reentry",
			fromAbove,
			hasArea && !fromAbove && !fromBelow,
			fromBelow,
			RuleSignalGroup.Zone,
			$"Above: a bar closed above VAH in the last {settings.EventLookbackBars} bars and price is back inside, so it often travels to VAL. Below: re-entered from below VAL, often travelling to VAH. In: no re-entry"
		);
	}

	private static RuleSignalRow AtrDistanceRow(string name, string key, double? level, double price, double? atr, double multiple)
	{
		bool known = level is not null && atr is > 0;
		double band = known ? multiple * atr!.Value : 0;
		double l = level ?? 0;
		return Tri(
			$"{name} ATR",
			"--color-badge-2",
			$"{ZonePrefix}{key}atr",
			known && price > l + band,
			known && Math.Abs(price - l) <= band,
			known && price < l - band,
			RuleSignalGroup.Zone,
			$"Above: price is more than {multiple:0.##} ATR above {name}. In: within {multiple:0.##} ATR of it. Below: more than {multiple:0.##} ATR below"
		);
	}

	private static IEnumerable<RuleSignalRow> RegimeRows(
		IReadOnlyList<HlocAvdtBar> bars,
		IReadOnlyList<HlocAvdtBar> windowBars,
		double? atr,
		IReadOnlyList<double> atrSeries,
		RuleSignalSettings settings
	)
	{
		var efficiency = MarketRegimeHelper.EfficiencyRatio(bars, settings.EfficiencyPeriod);
		yield return Banded(
			"EFFICIENCY",
			"--color-success",
			$"{RegimePrefix}er",
			efficiency,
			settings.EfficiencyTrendThreshold,
			settings.EfficiencyChopThreshold,
			RuleSignalGroup.Regime,
			$"Kaufman efficiency ratio over {settings.EfficiencyPeriod} bar closes. Above: trending (at least {settings.EfficiencyTrendThreshold:0.##}). Below: choppy (at most {settings.EfficiencyChopThreshold:0.##}). In: between"
		);

		var regression = MarketRegimeHelper.Regression(windowBars);
		double? riseInAtr =
			regression is { } fit && atr is > 0 ? fit.SlopePerBar * (windowBars.Count - 1) / atr.Value : null;
		yield return Banded(
			"REG SLOPE",
			"--color-badge-1",
			$"{RegimePrefix}slope",
			riseInAtr,
			settings.SlopeAtrThreshold,
			-settings.SlopeAtrThreshold,
			RuleSignalGroup.Regime,
			$"Regression line's rise across the window, in ATRs. Above: rising at least {settings.SlopeAtrThreshold:0.##} ATR. Below: falling as much. In: flat"
		);
		yield return Banded(
			"REG FIT",
			"--color-badge-1",
			$"{RegimePrefix}fit",
			regression?.RSquared,
			settings.RegressionFitHigh,
			settings.RegressionFitLow,
			RuleSignalGroup.Regime,
			$"How well the regression line fits the window (R²). Above: clean trend (at least {settings.RegressionFitHigh:0.##}). Below: noisy (at most {settings.RegressionFitLow:0.##}). In: between"
		);

		yield return Banded(
			"ATR PCTL",
			"--color-badge-3",
			$"{RegimePrefix}atrpct",
			MarketRegimeHelper.Percentile(atrSeries),
			settings.AtrPercentileHigh,
			settings.AtrPercentileLow,
			RuleSignalGroup.Regime,
			$"Where the current {settings.AtrPeriod}-bar ATR ranks among recent ATRs. Above: extreme (at least the {settings.AtrPercentileHigh:0}th percentile). Below: quiet (at most the {settings.AtrPercentileLow:0}th). In: normal"
		);

		yield return Banded(
			"VOL IMBAL",
			"--color-secondary",
			$"{RegimePrefix}imbal",
			MarketRegimeHelper.VolumeImbalance(windowBars),
			settings.VolumeImbalanceThreshold,
			-settings.VolumeImbalanceThreshold,
			RuleSignalGroup.Regime,
			$"Up-tick minus down-tick volume over the window, as a share of both. Above: buying dominates (at least {settings.VolumeImbalanceThreshold:0.###}). Below: selling dominates. In: balanced"
		);
	}

	private static IEnumerable<RuleSignalRow> SessionRows(RuleSignalMarket market, RuleSignalSettings settings)
	{
		bool inHours = market.TimestampUtc is { } time && IsWithinHours(time.Hour, settings.SessionStartHourUtc, settings.SessionEndHourUtc);
		yield return InOnly(
			"HOURS",
			"--color-tertiary",
			$"{SessionPrefix}hours-in",
			inHours,
			RuleSignalGroup.Session,
			$"The simulated time is within trading hours, {settings.SessionStartHourUtc:00}:00 to {settings.SessionEndHourUtc:00}:00 UTC -- the default skips the daily rollover and the thin late session"
		);

		bool known = market.Bid is not null && market.Ask is not null && market.TypicalSpread is > 0;
		double spread = known ? market.Ask!.Value - market.Bid!.Value : 0;
		double limit = known ? settings.SpreadMultiple * market.TypicalSpread!.Value : 0;
		yield return new RuleSignalRow(
			"SPREAD",
			"--color-error",
			$"{SessionPrefix}spread-above",
			$"{SessionPrefix}spread-in",
			null,
			known && spread > limit,
			known && spread <= limit,
			false,
			RuleSignalGroup.Session,
			$"Above: the spread is wider than {settings.SpreadMultiple:0.##} times its typical value. In: at or under it"
		);
	}

	/// <summary>Rows that depend on one account's open position -- each bot's own, or the selected trader's on screen.</summary>
	public static IReadOnlyList<RuleSignalRow> PositionRows(
		Position? position,
		double? bid,
		double? ask,
		DateTime? timestampUtc,
		TimeSpan barSize,
		RuleSignalSettings settings
	)
	{
		bool open = position is not null && bid is not null && ask is not null;
		var p = position.GetValueOrDefault();
		double unrealized = open ? p.UnrealizedPnl(bid!.Value, ask!.Value) : 0;
		double? risk = open && (p.InitialStopLossPrice ?? p.StopLossPrice) is { } stop ? Math.Abs(p.EntryPrice - stop) * p.Size : null;

		double? barsHeld =
			open && timestampUtc is { } now && barSize > TimeSpan.Zero ? (now - p.EntryTime) / barSize : null;
		var held = new RuleSignalRow(
			"BARS HELD",
			"--color-badge-3",
			$"{PositionPrefix}held-above",
			$"{PositionPrefix}held-in",
			null,
			barsHeld >= settings.MaxBarsHeld,
			barsHeld < settings.MaxBarsHeld,
			false,
			RuleSignalGroup.Position,
			$"Above: the position has been open {settings.MaxBarsHeld} bars or more (a time stop). In: fewer"
		);

		double? pnlR = risk is > 0 ? unrealized / risk.Value : null;
		var inR = Banded(
			"P/L (R)",
			"--color-success",
			$"{PositionPrefix}r",
			pnlR,
			settings.ProfitTargetR,
			-settings.LossLimitR,
			RuleSignalGroup.Position,
			$"Open P/L in R, the initial stop distance -- needs a stop loss. Above: at least +{settings.ProfitTargetR:0.##}R. Below: -{settings.LossLimitR:0.##}R or worse. In: between"
		);

		double peak = open ? p.PeakUnrealizedPnl : 0;
		double minPeak = risk is > 0 ? settings.GivebackMinPeakR * risk.Value : 0;
		bool gaveBack = open && peak > 0 && peak >= minPeak && (peak - unrealized) / peak >= settings.GivebackPercent / 100.0;
		var giveback = new RuleSignalRow(
			"GIVEBACK",
			"--color-error",
			$"{PositionPrefix}giveback-above",
			$"{PositionPrefix}giveback-in",
			null,
			gaveBack,
			open && !gaveBack,
			false,
			RuleSignalGroup.Position,
			$"Above: the position has given back {settings.GivebackPercent:0}% or more of its peak open profit, once that peak reached {settings.GivebackMinPeakR:0.##}R. In: not"
		);

		return new[] { held, inR, giveback };
	}

	private static bool IsWithinHours(int hour, int start, int end) =>
		start == end || (start < end ? hour >= start && hour < end : hour >= start || hour < end);

	private static bool HasSequence(IReadOnlyList<double> closes, Func<double, bool> first, Func<double, bool> then)
	{
		bool seenFirst = false;
		foreach (var close in closes)
		{
			if (seenFirst && then(close))
				return true;
			seenFirst |= first(close);
		}
		return false;
	}

	private static IEnumerable<HlocAvdtBar> Last(IReadOnlyList<HlocAvdtBar> bars, int count) =>
		bars.Skip(Math.Max(0, bars.Count - Math.Max(0, count)));

	private static RuleSignalRow Banded(
		string label,
		string colorVar,
		string keyPrefix,
		double? value,
		double high,
		double low,
		RuleSignalGroup group,
		string description
	) => Tri(label, colorVar, keyPrefix, value >= high, value is { } v && v < high && v > low, value <= low, group, description);

	private static RuleSignalRow Tri(
		string label,
		string colorVar,
		string keyPrefix,
		bool above,
		bool inBand,
		bool below,
		RuleSignalGroup group,
		string description
	) => new(label, colorVar, $"{keyPrefix}-above", $"{keyPrefix}-in", $"{keyPrefix}-below", above, inBand, below, group, description);

	private static RuleSignalRow InOnly(string label, string colorVar, string inKey, bool value, RuleSignalGroup group, string description) =>
		new(label, colorVar, null, inKey, null, false, value, false, group, description);
}
