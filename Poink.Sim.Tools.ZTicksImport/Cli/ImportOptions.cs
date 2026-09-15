using System;

namespace Poink.Sim.Tools.ZTicksImport.Cli;

internal sealed record ImportOptions(
	string CachePath,
	string SymbolName,
	bool IsLive,
	DateOnly? From,
	DateOnly? To,
	string? ConnectionString,
	bool DryRun
);
