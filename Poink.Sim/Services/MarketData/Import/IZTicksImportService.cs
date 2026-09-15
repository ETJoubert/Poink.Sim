using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poink.Sim.Services.MarketData.Import
{
	/// <summary>Reads cTrader's local .zticks backtesting cache and writes it through ITickIngest.</summary>
	internal interface IZTicksImportService
	{
		Task<IReadOnlyList<ZTicksPreviewEntry>> PreviewAsync(string cachePath);

		bool IsRunning { get; }
		ZTicksImportProgress? Progress { get; }
		ZTicksImportResult? LastResult { get; }
		string? LastError { get; }

		/// <summary>Raised on every progress update and on completion, from whichever thread is running the import.</summary>
		event Action? Changed;

		/// <summary>Starts an import in the background, surviving page navigation. Returns false if one is already running.</summary>
		bool StartImport(ZTicksImportRequest request);

		void CancelImport();
	}
}
