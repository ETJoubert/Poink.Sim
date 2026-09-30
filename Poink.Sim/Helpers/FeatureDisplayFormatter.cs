using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Helpers
{
	/// <summary>One FeatureModel value, flattened for display -- Section groups by feature source (XGridSignals/TxFold/TraderState).</summary>
	public sealed record FeatureDisplayRow(string Section, string Label, string Value);

	/// <summary>One TxFold field's live value, e.g. Field "O" Value "1.2345" -- see <see cref="FeatureDisplayFormatter.BuildTxFoldGrid"/>.</summary>
	public sealed record TxFoldGridCell(string Field, string Value);

	/// <summary>One TxFold grid row -- one per selected timeframe, plus a trailing Vol Profile/Current Price row when enabled.</summary>
	public sealed record TxFoldGridRow(string Label, IReadOnlyList<TxFoldGridCell> Cells);

	/// <summary>Shared by TrainPageV2's Playback (placeholder) and Live (real) tabs so both Features panels stay in sync.</summary>
	internal static class FeatureDisplayFormatter
	{
		// Reflection-driven so this panel doesn't need updating when FeatureModel's shape changes.
		public static IReadOnlyList<FeatureDisplayRow> Build(FeatureModel model, BarAggregationKind txFoldBarKind)
		{
			var rows = new List<FeatureDisplayRow>();
			foreach (var section in typeof(FeatureModel).GetProperties())
			{
				switch (section.GetValue(model))
				{
					case XGridSignalsFeature xGrid:
						foreach (var (key, value) in xGrid.SignalValues)
							rows.Add(new FeatureDisplayRow(section.Name, key, value.ToString("0.###")));
						break;
					case TxFoldFeature txFold:
						foreach (var (seconds, values) in txFold.ValuesBySeconds.OrderBy(kv => kv.Key))
						{
							var labels = TxFoldFieldLabels(txFoldBarKind, values.Length > 4);
							for (var i = 0; i < values.Length; i++)
								rows.Add(new FeatureDisplayRow(section.Name, $"{TimeframeLabel(seconds)} {labels[i]}", values[i].ToString("0.###")));
						}
						for (var i = 0; i < txFold.VolumeProfile.Count; i++)
							rows.Add(new FeatureDisplayRow(section.Name, $"Vol Profile [{i}]", txFold.VolumeProfile[i].ToString("0.###")));
						if (txFold.CurrentPrice is { } currentPrice)
							rows.Add(new FeatureDisplayRow(section.Name, "Current Price", currentPrice.ToString("0.###")));
						foreach (var (kind, value) in txFold.Levels.OrderBy(kv => kv.Key))
							rows.Add(new FeatureDisplayRow(section.Name, LevelLabel(kind), value.ToString("0.###")));
						break;
					case { } feature:
						foreach (var property in feature.GetType().GetProperties())
						{
							var value = property.GetValue(feature);
							if (value is not null)
								rows.Add(new FeatureDisplayRow(section.Name, property.Name, FormatFeatureValue(value)));
						}
						break;
				}
			}
			return rows;
		}

		// Same grid shape as TrainPageV2's Features tab preview (one row per timeframe, one cell per
		// field), but with live values instead of just field-name labels.
		public static IReadOnlyList<TxFoldGridRow> BuildTxFoldGrid(FeatureModel model, BarAggregationKind txFoldBarKind)
		{
			var txFold = model.TxFold;
			var rows = new List<TxFoldGridRow>();

			foreach (var (seconds, values) in txFold.ValuesBySeconds.OrderBy(kv => kv.Key))
			{
				var labels = TxFoldFieldLabels(txFoldBarKind, values.Length > 4);
				var cells = values.Select((value, i) => new TxFoldGridCell(labels[i], value.ToString("0.###"))).ToList();
				rows.Add(new TxFoldGridRow(TimeframeLabel(seconds), cells));
			}

			if (txFold.VolumeProfile.Count > 0)
			{
				var cells = txFold.VolumeProfile.Select((value, i) => new TxFoldGridCell($"[{i}]", value.ToString("0.###"))).ToList();
				rows.Add(new TxFoldGridRow("Vol Profile", cells));
			}

			if (txFold.CurrentPrice is { } currentPrice)
				rows.Add(new TxFoldGridRow("Current Price", [new TxFoldGridCell("px", currentPrice.ToString("0.###"))]));

			if (txFold.Levels.Count > 0)
			{
				var cells = txFold.Levels.OrderBy(kv => kv.Key).Select(kv => new TxFoldGridCell(LevelLabel(kv.Key), kv.Value.ToString("0.###"))).ToList();
				rows.Add(new TxFoldGridRow("Levels", cells));
			}

			return rows;
		}

		public static string TimeframeLabel(int seconds) => SnapshotTimeframeOptions.All.FirstOrDefault(o => o.Seconds == seconds).Label;

		public static string LevelLabel(TxFoldLevelKind kind) => TxFoldLevelOptions.All.First(o => o.Kind == kind).Label;

		// Every kind stores exactly 4 values per bar (6 when IncludeHighLow adds the window's true High/Low) -- these just relabel the slots.
		public static string[] TxFoldFieldLabels(BarAggregationKind kind, bool includeHighLow = false)
		{
			string[] baseLabels = kind switch
			{
				BarAggregationKind.Quartile => ["Q1", "Max", "Min", "Q3"],
				BarAggregationKind.Decile => ["D1", "Max", "Min", "D9"],
				BarAggregationKind.MeanStdDev => ["Mean", "+SD", "-SD", "Mean"],
				_ => ["O", "H", "L", "C"],
			};
			return includeHighLow ? [.. baseLabels, "TrueH", "TrueL"] : baseLabels;
		}

		private static string FormatFeatureValue(object value) =>
			value switch
			{
				IReadOnlyList<double> list => string.Join(", ", list.Select(v => v.ToString("0.###"))),
				double number => number.ToString("0.###"),
				_ => value.ToString() ?? "",
			};
	}
}
