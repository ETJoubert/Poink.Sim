using System;
using System.Collections.Generic;

namespace Poink.Sim.Services.MarketData.Import
{
	/// <summary>One scanned .zticks file, parsed but not yet written, shown on the Data page before import.</summary>
	internal sealed record ZTicksPreviewEntry(
		string File,
		DateOnly? Date,
		int TickCount,
		DateTime? MinUtc,
		DateTime? MaxUtc
	);

	/// <summary>
	/// Parameters for one import run: where to read from, and which tick table to write into. Always writes
	/// to the live table -- see TickTableNaming. OnlyDates, when set, restricts the run to those calendar
	/// days (e.g. a diff-only import), instead of every file the cache path scans to.
	/// </summary>
	internal sealed record ZTicksImportRequest(
		string CachePath,
		string SymbolName,
		bool CreateTableIfMissing,
		long SymbolCode,
		IReadOnlySet<DateOnly>? OnlyDates = null
	);

	/// <summary>Progress after one file of an import run, for live UI updates.</summary>
	internal sealed record ZTicksImportProgress(
		int FilesDone,
		int FilesTotal,
		string CurrentFile,
		long TicksParsed,
		long RowsWritten
	);

	/// <summary>Final outcome of an import run.</summary>
	internal sealed record ZTicksImportResult(int FilesProcessed, long TicksParsed, long RowsWritten, string Table);
}
