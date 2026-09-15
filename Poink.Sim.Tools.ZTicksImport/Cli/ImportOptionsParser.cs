using System;
using System.Globalization;

namespace Poink.Sim.Tools.ZTicksImport.Cli;

internal static class ImportOptionsParser
{
	public const string HelpText = """
        Imports cTrader .zticks backtesting cache files into Poink.Sim's tick store.

        Usage:
          Poink.Sim.Tools.ZTicksImport --cache <path> --symbol <name> --env live|demo [options]

        Required:
          --cache <path>            A single .zticks file, or a directory of them (e.g. the broker's t1 folder).
          --symbol <name>           Symbol name as tracked by Poink.Sim (used for the live_ticks_/demo_ticks_ table name).
          --env live|demo           Which Poink.Sim tick table set to import into.

        Optional:
          --from <yyyy-MM-dd>       Skip files dated before this (by filename).
          --to <yyyy-MM-dd>         Skip files dated after this (by filename).
          --connection-string <s>   Overrides the resolved PoinkMarketData connection string.
          --dry-run                 Parse and report counts only; makes no database connection or writes.
          --help                    Show this text.
        """;

	public static ImportOptions Parse(string[] args)
	{
		string? cachePath = null;
		string? symbolName = null;
		bool? isLive = null;
		DateOnly? from = null;
		DateOnly? to = null;
		string? connectionString = null;
		var dryRun = false;

		for (var i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--cache":
					cachePath = RequireValue(args, ref i, "--cache");
					break;
				case "--symbol":
					symbolName = RequireValue(args, ref i, "--symbol");
					break;
				case "--env":
					isLive = ParseEnvironment(RequireValue(args, ref i, "--env"));
					break;
				case "--from":
					from = ParseDate(RequireValue(args, ref i, "--from"));
					break;
				case "--to":
					to = ParseDate(RequireValue(args, ref i, "--to"));
					break;
				case "--connection-string":
					connectionString = RequireValue(args, ref i, "--connection-string");
					break;
				case "--dry-run":
					dryRun = true;
					break;
				default:
					throw new ArgumentException($"Unrecognized argument: {args[i]}");
			}
		}

		if (cachePath is null)
			throw new ArgumentException("--cache is required.");
		if (symbolName is null)
			throw new ArgumentException("--symbol is required.");
		if (isLive is null)
			throw new ArgumentException("--env is required (live or demo).");

		return new ImportOptions(cachePath, symbolName, isLive.Value, from, to, connectionString, dryRun);
	}

	private static string RequireValue(string[] args, ref int index, string flag)
	{
		if (index + 1 >= args.Length)
			throw new ArgumentException($"{flag} requires a value.");
		return args[++index];
	}

	private static bool ParseEnvironment(string value) =>
		value.ToLowerInvariant() switch
		{
			"live" => true,
			"demo" => false,
			_ => throw new ArgumentException($"--env must be 'live' or 'demo', got '{value}'."),
		};

	private static DateOnly ParseDate(string value) =>
		DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
			? date
			: throw new ArgumentException($"Could not parse date '{value}'. Use yyyy-MM-dd.");
}
