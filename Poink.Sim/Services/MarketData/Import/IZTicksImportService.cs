using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.MarketData.Import
{
	/// <summary>Reads cTrader's local .zticks backtesting cache and writes it through ITickIngest.</summary>
	internal interface IZTicksImportService
	{
		Task<IReadOnlyList<ZTicksPreviewEntry>> PreviewAsync(string cachePath, DateOnly? from, DateOnly? to);

		Task<ZTicksImportResult> ImportAsync(
			ZTicksImportRequest request,
			Action<ZTicksImportProgress>? onProgress,
			CancellationToken cancellationToken = default
		);
	}
}
