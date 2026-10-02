using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.RuleEngine
{
	/// <summary>Edits the <see cref="RuleSignalSettings"/> every rule set's event, zone, regime, session and position signals read.</summary>
	public partial class RuleSignalSettingsPanel : ComponentBase
	{
		private sealed record SettingField(
			string Label,
			string Tooltip,
			Func<RuleSignalSettings, double> Get,
			Func<RuleSignalSettings, double, RuleSignalSettings> Set,
			double Step,
			double Min,
			double? Max = null
		);

		private sealed record SettingSection(string Label, string Title, IReadOnlyList<SettingField> Fields);

		private static readonly SettingSection[] Sections =
		{
			new(
				"EVENTS",
				"Crosses, reclaims, value-area re-entries and first touches",
				new SettingField[]
				{
					new("Lookback bars", "Completed bars looked back over for crosses, reclaims and value-area re-entries", s => s.EventLookbackBars, (s, v) => s with { EventLookbackBars = (int)v }, 1, 1),
					new("First touch bars", "Completed bars that must not have touched VAH/VAL for a touch now to count as the first", s => s.FirstTouchLookbackBars, (s, v) => s with { FirstTouchLookbackBars = (int)v }, 1, 1),
				}
			),
			new(
				"ATR",
				"Average true range, used by the zone distance, regression slope and ATR percentile signals",
				new SettingField[]
				{
					new("ATR period", "Bars averaged into the average true range", s => s.AtrPeriod, (s, v) => s with { AtrPeriod = (int)v }, 1, 1),
					new("Stretch (ATR)", "How many ATRs from VWAP or POC count as stretched", s => s.AtrDistanceMultiple, (s, v) => s with { AtrDistanceMultiple = v }, 0.1, 0),
				}
			),
			new(
				"REGIME",
				"Trend, volatility and order-flow filters",
				new SettingField[]
				{
					new("Efficiency period", "Bar closes the efficiency ratio is measured over", s => s.EfficiencyPeriod, (s, v) => s with { EfficiencyPeriod = (int)v }, 1, 2),
					new("Trending at", "Efficiency ratio at or above which the market counts as trending -- a random walk sits near 0.3", s => s.EfficiencyTrendThreshold, (s, v) => s with { EfficiencyTrendThreshold = v }, 0.05, 0, 1),
					new("Choppy at", "Efficiency ratio at or below which the market counts as choppy", s => s.EfficiencyChopThreshold, (s, v) => s with { EfficiencyChopThreshold = v }, 0.05, 0, 1),
					new("Slope (ATR)", "Regression rise over the window, in ATRs, beyond which the slope counts as up or down rather than flat", s => s.SlopeAtrThreshold, (s, v) => s with { SlopeAtrThreshold = v }, 0.1, 0),
					new("Clean fit R²", "Regression R² at or above which the trend line fits cleanly", s => s.RegressionFitHigh, (s, v) => s with { RegressionFitHigh = v }, 0.05, 0, 1),
					new("Noisy fit R²", "Regression R² at or below which the trend line fits poorly", s => s.RegressionFitLow, (s, v) => s with { RegressionFitLow = v }, 0.05, 0, 1),
					new("Extreme ATR pctl", "ATR percentile at or above which volatility counts as extreme", s => s.AtrPercentileHigh, (s, v) => s with { AtrPercentileHigh = v }, 1, 0, 100),
					new("Quiet ATR pctl", "ATR percentile at or below which volatility counts as quiet", s => s.AtrPercentileLow, (s, v) => s with { AtrPercentileLow = v }, 1, 0, 100),
					new("Volume imbalance", "Up-minus-down tick volume over the window, as a share of both, beyond which buying or selling dominates", s => s.VolumeImbalanceThreshold, (s, v) => s with { VolumeImbalanceThreshold = v }, 0.01, 0, 1),
				}
			),
			new(
				"SESSION",
				"Trading hours and spread",
				new SettingField[]
				{
					new("Start hour (UTC)", "UTC hour trading may start, inclusive", s => s.SessionStartHourUtc, (s, v) => s with { SessionStartHourUtc = (int)v }, 1, 0, 23),
					new("End hour (UTC)", "UTC hour trading stops, exclusive -- earlier than the start wraps past midnight", s => s.SessionEndHourUtc, (s, v) => s with { SessionEndHourUtc = (int)v }, 1, 0, 23),
					new("Spread multiple", "How many times its typical value the spread may be and still count as normal", s => s.SpreadMultiple, (s, v) => s with { SpreadMultiple = v }, 0.1, 0),
				}
			),
			new(
				"POSITION",
				"Close-rule signals about the open position",
				new SettingField[]
				{
					new("Max bars held", "Bars held at or beyond which a position counts as overdue (a time stop)", s => s.MaxBarsHeld, (s, v) => s with { MaxBarsHeld = (int)v }, 1, 1),
					new("Profit target (R)", "Open profit, in R (multiples of the initial stop distance), at or above which a position counts as a winner", s => s.ProfitTargetR, (s, v) => s with { ProfitTargetR = v }, 0.1, 0),
					new("Loss limit (R)", "Open loss, in R, at or beyond which a position counts as a loser -- entered as a positive number", s => s.LossLimitR, (s, v) => s with { LossLimitR = v }, 0.1, 0),
					new("Giveback %", "Share of a position's peak open profit that counts as given back", s => s.GivebackPercent, (s, v) => s with { GivebackPercent = v }, 1, 0, 100),
					new("Giveback min peak (R)", "Smallest peak open profit, in R, before giveback is judged, so a trade that barely went green is not cut", s => s.GivebackMinPeakR, (s, v) => s with { GivebackMinPeakR = v }, 0.1, 0),
				}
			),
		};

		[Parameter]
		public RuleSignalSettings Settings { get; set; } = new();

		[Parameter]
		public EventCallback<RuleSignalSettings> SettingsChanged { get; set; }

		private Task SetField(SettingField field, ChangeEventArgs e)
		{
			if (!double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
				return Task.CompletedTask;
			value = Math.Max(field.Min, field.Max is { } max ? Math.Min(max, value) : value);
			return SettingsChanged.InvokeAsync(field.Set(Settings, value));
		}

		private Task ResetToDefaults() => SettingsChanged.InvokeAsync(new RuleSignalSettings());
	}
}
