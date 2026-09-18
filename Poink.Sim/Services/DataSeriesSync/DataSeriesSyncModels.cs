using System;
using System.Collections.Generic;

namespace Poink.Sim.Services.DataSeriesSync;

/// <summary>A day's place in the gap between the tick store's latest coverage and today.</summary>
public enum DataSeriesSyncDayStatus
{
	InDatabase,
	AvailableToLoad,
	CurrentDay,
}

public readonly record struct DataSeriesSyncDay(DateOnly Date, DataSeriesSyncDayStatus Status);

/// <summary>
/// Gap view for one symbol's tick table: the latest day already in the database through today,
/// with every day in between marked as available to load from cTrader.
/// </summary>
public sealed record DataSeriesSyncRange(
	string SymbolName,
	bool HasData,
	DateOnly? LatestDatabaseDate,
	DateOnly CurrentDate,
	IReadOnlyList<DataSeriesSyncDay> Days
);
