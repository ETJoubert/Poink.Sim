using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Tools.ZTicksImport.Cli;
using Poink.Sim.Tools.ZTicksImport.Storage;
using Poink.ZTicks;
using Serilog;

if (args.Contains("--help") || args.Length == 0)
{
	Console.WriteLine(ImportOptionsParser.HelpText);
	return 0;
}

ImportOptions options;
try
{
	options = ImportOptionsParser.Parse(args);
}
catch (ArgumentException ex)
{
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine();
	Console.Error.WriteLine(ImportOptionsParser.HelpText);
	return 1;
}

Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();
using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger, dispose: true));
var logger = loggerFactory.CreateLogger("ZTicksImport");

try
{
	var files = ZTicksCacheScanner.FindFiles(options.CachePath, options.From, options.To);
	if (files.Count == 0)
	{
		logger.LogWarning("No .zticks files found under {CachePath} for the given range.", options.CachePath);
		return 0;
	}

	logger.LogInformation("Found {Count} .zticks file(s) under {CachePath}.", files.Count, options.CachePath);

	var table = TickTableNaming.ForSymbol(options.SymbolName, options.IsLive);
	logger.LogInformation(
		"Target table: {Table} (env={Env}, symbol={Symbol}, dryRun={DryRun}).",
		table,
		options.IsLive ? "live" : "demo",
		options.SymbolName,
		options.DryRun
	);

	TickStore? store = null;
	if (!options.DryRun)
	{
		var connectionString = ConnectionStringResolver.Resolve(options.ConnectionString);
		store = new TickStore(connectionString, loggerFactory.CreateLogger<TickStore>());
		store.EnsureTable(table);
		store.AssociateSymbol(table, options.SymbolName);
	}

	long totalParsed = 0;
	long totalWritten = 0;
	DateTime? overallMin = null;
	DateTime? overallMax = null;

	foreach (var file in files)
	{
		var ticks = ZTicksFileReader.Read(file);
		totalParsed += ticks.Count;

		if (ticks.Count == 0)
		{
			logger.LogInformation("{File}: 0 ticks, skipped.", file);
			continue;
		}

		overallMin = overallMin is null ? ticks[0].TimestampUtc : Min(overallMin.Value, ticks[0].TimestampUtc);
		overallMax = overallMax is null ? ticks[^1].TimestampUtc : Max(overallMax.Value, ticks[^1].TimestampUtc);

		if (options.DryRun)
		{
			logger.LogInformation(
				"{File}: parsed {Count} ticks ({Min:u}..{Max:u}).",
				file,
				ticks.Count,
				ticks[0].TimestampUtc,
				ticks[^1].TimestampUtc
			);
			continue;
		}

		var written = store!.InsertTicks(table, ticks);
		totalWritten += written;
		logger.LogInformation(
			"{File}: parsed {Parsed} ticks, wrote {Written} new row(s) to {Table}.",
			file,
			ticks.Count,
			written,
			table
		);
	}

	if (!options.DryRun && overallMin is not null && overallMax is not null)
		store!.LogBackfill(table, options.SymbolName, overallMin.Value, overallMax.Value, totalWritten);

	logger.LogInformation(
		"Done. {FileCount} file(s), {Parsed} tick(s) parsed, {Written} row(s) written.",
		files.Count,
		totalParsed,
		options.DryRun ? 0 : totalWritten
	);

	return 0;
}
catch (Exception ex)
{
	logger.LogError(ex, "Import failed.");
	return 1;
}

static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
