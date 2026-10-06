using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Poink.Sim.Repository;

namespace Poink.Sim.Helpers;

/// <summary>
/// Reads one tick range that may span two tables: [from, <c>historyBeforeUtc</c>) from a history table and the rest from
/// the main table. The Trade page needs this on a demo account, whose history lives in the live table while today's
/// ticks are recorded in the demo one.
/// </summary>
internal static class SplitTickReadHelper
{
	private const string UndefinedTableSqlState = "42P01";

	public static List<Tick> Read(
		ITickRepository repository,
		string table,
		string? historyTable,
		DateTime? historyBeforeUtc,
		DateTime fromUtc,
		DateTime toUtc
	)
	{
		if (string.IsNullOrWhiteSpace(historyTable) || historyTable == table || historyBeforeUtc is not { } boundary)
			return repository.ReadTicks(table, fromUtc, toUtc).ToList();

		var ticks = new List<Tick>();
		if (fromUtc < boundary)
			ticks.AddRange(ReadIfExists(repository, historyTable, fromUtc, Min(toUtc, boundary)));
		if (toUtc > boundary)
			ticks.AddRange(ReadIfExists(repository, table, Max(fromUtc, boundary), toUtc));
		return ticks;
	}

	// Either side of a split may not exist yet (e.g. no demo ticks recorded so far), which reads as no ticks rather than an error.
	private static List<Tick> ReadIfExists(ITickRepository repository, string table, DateTime fromUtc, DateTime toUtc)
	{
		try
		{
			return repository.ReadTicks(table, fromUtc, toUtc).ToList();
		}
		catch (PostgresException ex) when (ex.SqlState == UndefinedTableSqlState)
		{
			return new List<Tick>();
		}
	}

	private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

	private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
