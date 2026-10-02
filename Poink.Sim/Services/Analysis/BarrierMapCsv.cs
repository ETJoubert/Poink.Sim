using System.Globalization;
using System.Text;
using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

/// <summary>Writes every cell of a barrier map -- each side and time limit -- as CSV.</summary>
internal static class BarrierMapCsv
{
	public static string Build(BarrierMapResult result)
	{
		var builder = new StringBuilder();
		builder.AppendLine(
			"side,time_limit_min,stop_pips,target_pips,trades,wins,losses,timeouts,win_rate,scrambled_win_rate,edge,mean_pips,scrambled_mean_pips,edge_pips,ci_pips,mean_minutes"
		);
		foreach (var grid in result.Grids)
			foreach (var cell in grid.Cells)
				builder.AppendLine(
					string.Join(
						",",
						grid.Side.ToString(),
						Number(grid.TimeLimitMinutes),
						Number(cell.StopPips),
						Number(cell.TargetPips),
						Number(cell.Trades),
						Number(cell.Wins),
						Number(cell.Losses),
						Number(cell.Timeouts),
						Number(cell.WinRate),
						Number(cell.BenchmarkWinRate),
						Number(cell.Edge),
						Number(cell.MeanPips),
						Number(cell.BenchmarkMeanPips),
						Number(cell.EdgePips),
						Number(cell.CiPips),
						Number(cell.MeanMinutes)
					)
				);
		return builder.ToString();
	}

	private static string Number(double? value) => value?.ToString("G6", CultureInfo.InvariantCulture) ?? "";
}
