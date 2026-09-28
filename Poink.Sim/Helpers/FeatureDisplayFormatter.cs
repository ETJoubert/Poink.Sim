using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Helpers
{
	/// <summary>One FeatureModel value, flattened for display -- Section groups by feature source (XGridSignals/TxFold/TraderState).</summary>
	public sealed record FeatureDisplayRow(string Section, string Label, string Value);

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
							var labels = TxFoldFieldLabels(txFoldBarKind);
							for (var i = 0; i < values.Length; i++)
								rows.Add(new FeatureDisplayRow(section.Name, $"{TimeframeLabel(seconds)} {labels[i]}", values[i].ToString("0.###")));
						}
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

		public static string TimeframeLabel(int seconds) => SnapshotTimeframeOptions.All.FirstOrDefault(o => o.Seconds == seconds).Label;

		// Every kind stores exactly 4 values per bar -- these just relabel the same 4 slots.
		public static string[] TxFoldFieldLabels(BarAggregationKind kind) =>
			kind switch
			{
				BarAggregationKind.Quartile => ["Q1", "Max", "Min", "Q3"],
				BarAggregationKind.Decile => ["D1", "Max", "Min", "D9"],
				BarAggregationKind.MeanStdDev => ["Mean", "+SD", "-SD", "Mean"],
				_ => ["O", "H", "L", "C"],
			};

		private static string FormatFeatureValue(object value) =>
			value switch
			{
				IReadOnlyList<double> list => string.Join(", ", list.Select(v => v.ToString("0.###"))),
				double number => number.ToString("0.###"),
				_ => value.ToString() ?? "",
			};
	}
}
