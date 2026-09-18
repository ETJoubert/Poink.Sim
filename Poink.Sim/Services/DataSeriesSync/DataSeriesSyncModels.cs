using System;
using System.Collections.Generic;

namespace Poink.Sim.Services.DataSeriesSync;

/// <summary>A day's place in the gap between the tick store's latest coverage and today.</summary>
public enum DataSeriesSyncDayStatus
{
	InDatabase,
	AvailableInSource,
	AvailableToLoad,
	CurrentDay,
}

public readonly record struct DataSeriesSyncDay(DateOnly Date, DataSeriesSyncDayStatus Status);

/// <summary>
/// Gap view for one symbol's tick table: the latest day already in the database through today.
/// Days found as a file in the registry's saved import SourceDirectory are AvailableInSource; the
/// rest are AvailableToLoad (unverified -- would need a live API fetch).
/// </summary>
public sealed record DataSeriesSyncRange(
	string SymbolName,
	bool HasData,
	DateOnly? LatestDatabaseDate,
	DateOnly CurrentDate,
	string? SourceDirectory,
	IReadOnlyList<DataSeriesSyncDay> Days
);
