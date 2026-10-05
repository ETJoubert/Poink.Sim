using System;
using Poink.Sim.Services.MarketData.Import;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports the Data page's .zticks import, with progress counted in files.</summary>
internal sealed class DataImportActivitySource : IActivitySource, IDisposable
{
	private readonly IZTicksImportService _import;

	public DataImportActivitySource(IZTicksImportService import)
	{
		_import = import;
		_import.Changed += RaiseChanged;
	}

	public string Name => "Tick import";
	public string Route => "/data";

	public ActivityState Current =>
		_import.IsRunning
			? new ActivityState(true, _import.Progress is { FilesTotal: > 0 } p ? 100.0 * p.FilesDone / p.FilesTotal : null)
			: ActivityState.Idle;

	public ActivityResult LastResult =>
		_import switch
		{
			{ WasCancelled: true } => new ActivityResult(ActivityOutcome.Stopped),
			{ LastError: { } error } => new ActivityResult(ActivityOutcome.Failed, error),
			{ LastResult: { } result } => new ActivityResult(
				ActivityOutcome.Completed,
				$"{result.FilesProcessed:N0} files, {result.RowsWritten:N0} rows into {result.Table}"
			),
			_ => new ActivityResult(ActivityOutcome.Stopped),
		};

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose() => _import.Changed -= RaiseChanged;
}
