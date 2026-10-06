using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>
	/// Maps cTrader symbols to their stored tick history. Coverage is judged on the live table because Trade page history
	/// before today always reads it; "current" means it reaches the last weekday before today, the newest day the Data
	/// page import can supply.
	/// </summary>
	public static class SymbolCoverageHelper
	{
		public static IReadOnlyDictionary<string, SymbolTickCoverage> Build(
			IEnumerable<(string Table, long Rows, DateTime? MinTime, DateTime? MaxTime)> registry,
			DateTime nowUtc
		)
		{
			var expectedFrom = LastExpectedDayStartUtc(nowUtc);
			return registry
				.GroupBy(r => r.Table)
				.ToDictionary(
					g => g.Key,
					g =>
					{
						var row = g.First();
						return new SymbolTickCoverage(row.Table, row.Rows, row.MinTime, row.MaxTime, StatusOf(row.Rows, row.MaxTime, expectedFrom));
					}
				);
		}

		public static SymbolTickCoverage For(string symbolName, IReadOnlyDictionary<string, SymbolTickCoverage> coverage)
		{
			var table = TickTableNaming.ForSymbol(symbolName, isLive: true);
			return coverage.TryGetValue(table, out var found) ? found : new SymbolTickCoverage(table, 0, null, null, SymbolCoverageStatus.None);
		}

		/// <summary>Start of the most recent Mon-Fri day strictly before <paramref name="nowUtc"/>'s day.</summary>
		public static DateTime LastExpectedDayStartUtc(DateTime nowUtc)
		{
			var day = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc).AddDays(-1);
			while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
				day = day.AddDays(-1);
			return day;
		}

		public static string Describe(SymbolTickCoverage coverage) =>
			coverage.Status switch
			{
				SymbolCoverageStatus.None => $"No stored ticks ({coverage.Table}) -- import them from the Data page",
				SymbolCoverageStatus.Stale => $"Stored ticks {RangeLabel(coverage)}, {coverage.Rows:N0} rows -- behind; import recent days from the Data page",
				_ => $"Stored ticks {RangeLabel(coverage)}, {coverage.Rows:N0} rows -- up to date",
			};

		private static SymbolCoverageStatus StatusOf(long rows, DateTime? maxUtc, DateTime expectedFrom) =>
			rows <= 0 || maxUtc is not { } max ? SymbolCoverageStatus.None
			: max >= expectedFrom ? SymbolCoverageStatus.Current
			: SymbolCoverageStatus.Stale;

		private static string RangeLabel(SymbolTickCoverage coverage) =>
			coverage.MinUtc is { } min && coverage.MaxUtc is { } max ? $"{min:yyyy-MM-dd} → {max:yyyy-MM-dd}" : "(no range recorded)";
	}
}
