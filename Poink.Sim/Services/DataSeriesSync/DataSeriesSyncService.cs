using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Services.MarketData;
using Poink.ZTicks;

namespace Poink.Sim.Services.DataSeriesSync;

/// <summary>Reads coverage off ITickIngest's registry -- write/backfill paths live elsewhere.</summary>
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
		string? sourceDirectory;
		try
		{
			coverage = _tickIngest.GetCoverage(table);
			sourceDirectory = _tickIngest.GetSourcePath(table);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Coverage lookup failed for {Table}; showing no database coverage.", table);
			return NoDataRange(symbolName, currentDate);
		}

		if (!coverage.TableExists || coverage.MaxUtc is null)
			return NoDataRange(symbolName, currentDate);

		var latestDatabaseDate = DateOnly.FromDateTime(coverage.MaxUtc.Value);
		var sourceDates = ReadSourceDates(sourceDirectory);
		return new DataSeriesSyncRange(
			symbolName,
			true,
			latestDatabaseDate,
			currentDate,
			sourceDirectory,
			BuildDays(latestDatabaseDate, currentDate, sourceDates)
		);
	}

	private HashSet<DateOnly> ReadSourceDates(string? sourceDirectory)
	{
		if (string.IsNullOrWhiteSpace(sourceDirectory))
			return new HashSet<DateOnly>();

		try
		{
			return ZTicksCacheScanner.FindDates(sourceDirectory).ToHashSet();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to scan saved source directory {Directory} for a diff.", sourceDirectory);
			return new HashSet<DateOnly>();
		}
	}

	private static DataSeriesSyncRange NoDataRange(string symbolName, DateOnly currentDate) =>
		new(symbolName, false, null, currentDate, null, Array.Empty<DataSeriesSyncDay>());

	private static List<DataSeriesSyncDay> BuildDays(DateOnly latestDatabaseDate, DateOnly currentDate, HashSet<DateOnly> sourceDates)
	{
		var days = new List<DataSeriesSyncDay> { new(latestDatabaseDate, DataSeriesSyncDayStatus.InDatabase) };
		if (latestDatabaseDate >= currentDate)
			return days;

		for (var date = latestDatabaseDate.AddDays(1); date < currentDate; date = date.AddDays(1))
		{
			var status = sourceDates.Contains(date) ? DataSeriesSyncDayStatus.AvailableInSource : DataSeriesSyncDayStatus.AvailableToLoad;
			days.Add(new DataSeriesSyncDay(date, status));
		}

		days.Add(new DataSeriesSyncDay(currentDate, DataSeriesSyncDayStatus.CurrentDay));
		return days;
	}
}
