using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Services.DataSeriesSync;

/// <summary>Reads coverage off ITickIngest's registry rather than backfilling anything itself -- see TickBackfillWorker for the actual sync/write path.</summary>
internal sealed class DataSeriesSyncService : IDataSeriesSyncService
{
	private readonly ITickIngest _tickIngest;
	private readonly ILogger<DataSeriesSyncService> _logger;

	public DataSeriesSyncService(ITickIngest tickIngest, ILogger<DataSeriesSyncService> logger)
	{
		_tickIngest = tickIngest;
		_logger = logger;
	}

	public DataSeriesSyncRange GetSyncRange(string symbolName)
	{
		// Always the live table -- demo price feeds diverge from the real market, so training/
		// simulation never uses demo_ticks_, regardless of which environment is connected.
		var table = TickTableNaming.ForSymbol(symbolName, isLive: true);
		var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

		TickCoverage coverage;
		try
		{
			coverage = _tickIngest.GetCoverage(table);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Coverage lookup failed for {Table}; showing no database coverage.", table);
			return NoDataRange(symbolName, currentDate);
		}

		if (!coverage.TableExists || coverage.MaxUtc is null)
			return NoDataRange(symbolName, currentDate);

		var latestDatabaseDate = DateOnly.FromDateTime(coverage.MaxUtc.Value);
		return new DataSeriesSyncRange(symbolName, true, latestDatabaseDate, currentDate, BuildDays(latestDatabaseDate, currentDate));
	}

	private static DataSeriesSyncRange NoDataRange(string symbolName, DateOnly currentDate) =>
		new(symbolName, false, null, currentDate, Array.Empty<DataSeriesSyncDay>());

	private static List<DataSeriesSyncDay> BuildDays(DateOnly latestDatabaseDate, DateOnly currentDate)
	{
		var days = new List<DataSeriesSyncDay> { new(latestDatabaseDate, DataSeriesSyncDayStatus.InDatabase) };
		if (latestDatabaseDate >= currentDate)
			return days;

		for (var date = latestDatabaseDate.AddDays(1); date < currentDate; date = date.AddDays(1))
			days.Add(new DataSeriesSyncDay(date, DataSeriesSyncDayStatus.AvailableToLoad));

		days.Add(new DataSeriesSyncDay(currentDate, DataSeriesSyncDayStatus.CurrentDay));
		return days;
	}
}
