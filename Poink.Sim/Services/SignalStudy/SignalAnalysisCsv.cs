using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>Writes an analysis result as CSV: the baseline, every ranked single and every ranked pair, with all horizons.</summary>
internal static class SignalAnalysisCsv
{
	public static string Build(SignalAnalysisResult result)
	{
		var builder = new StringBuilder();
		var horizonColumns = result.Options.HorizonMinutes.SelectMany(h => new[] { $"move_{h}m_atr", $"ci_{h}m_atr", $"lift_{h}m_atr" });
		builder.AppendLine(
			string.Join(
				",",
				new[] { "kind", "signal", "key", "group", "entries" }
					.Concat(horizonColumns)
					.Concat(new[] { "long_win", "long_r", "short_win", "short_r", "best_side", "t", "first_half_r", "second_half_r", "spread_pips" })
			)
		);

		AppendRow(builder, "baseline", result.Baseline);
		foreach (var single in result.Singles)
			AppendRow(builder, "single", single);
		foreach (var pair in result.Pairs)
			AppendRow(builder, "pair", pair);
		return builder.ToString();
	}

	private static void AppendRow(StringBuilder builder, string kind, SignalStateStats row)
	{
		var cells = new List<string> { kind, Quote(row.Name), Quote(row.Key), row.Group?.ToString() ?? "", row.Entries.ToString(CultureInfo.InvariantCulture) };
		for (int h = 0; h < row.MeanForwardAtr.Count; h++)
		{
			cells.Add(Number(row.MeanForwardAtr[h]));
			cells.Add(Number(row.ForwardCiAtr[h]));
			cells.Add(Number(row.LiftAtr[h]));
		}
		cells.AddRange(
			new[]
			{
				Number(row.LongWinRate),
				Number(row.LongExpectancyR),
				Number(row.ShortWinRate),
				Number(row.ShortExpectancyR),
				row.BestSide.ToString(),
				Number(row.BestTStat),
				Number(row.FirstHalfR),
				Number(row.SecondHalfR),
				Number(row.MeanSpreadPips),
			}
		);
		builder.AppendLine(string.Join(",", cells));
	}

	private static string Number(double? value) => value?.ToString("G6", CultureInfo.InvariantCulture) ?? "";

	private static string Quote(string text) => $"\"{text.Replace("\"", "\"\"")}\"";
}
