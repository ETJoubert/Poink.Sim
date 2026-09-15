using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;
using Poink.ZTicks;

namespace Poink.Sim.Services.MarketData.Import
{
	/// <summary>Writes through the same ITickIngest the live backfill worker uses, so imported history is indistinguishable from streamed ticks.</summary>
	internal sealed class ZTicksImportService : IZTicksImportService
	{
		private readonly ITickIngest _ingest;
		private readonly ILogger<ZTicksImportService> _logger;

		public ZTicksImportService(ITickIngest ingest, ILogger<ZTicksImportService> logger)
		{
			_ingest = ingest;
			_logger = logger;
		}

		public Task<IReadOnlyList<ZTicksPreviewEntry>> PreviewAsync(string cachePath, DateOnly? from, DateOnly? to) =>
			Task.Run<IReadOnlyList<ZTicksPreviewEntry>>(() =>
			{
				var files = ZTicksCacheScanner.FindFiles(cachePath, from, to);
				return files
					.Select(file =>
					{
						var ticks = ZTicksFileReader.Read(file);
						return new ZTicksPreviewEntry(
							file,
							ZTicksCacheScanner.TryParseFileDate(file),
							ticks.Count,
							ticks.Count > 0 ? ticks[0].TimestampUtc : null,
							ticks.Count > 0 ? ticks[^1].TimestampUtc : null
						);
					})
					.ToList();
			});

		public async Task<ZTicksImportResult> ImportAsync(
			ZTicksImportRequest request,
			Action<ZTicksImportProgress>? onProgress,
			CancellationToken cancellationToken = default
		)
		{
			var files = ZTicksCacheScanner.FindFiles(request.CachePath, request.From, request.To);
			var table = TickTableNaming.ForSymbol(request.SymbolName, request.IsLive);

			if (!_ingest.TableExists(table))
			{
				if (!request.CreateTableIfMissing)
					throw new InvalidOperationException($"Table {table} does not exist and creating it was not requested.");
				_ingest.CreateTable(table);
			}

			_ingest.AssociateSymbol(table, request.SymbolName);

			long parsed = 0;
			long written = 0;
			DateTime? minUtc = null;
			DateTime? maxUtc = null;

			for (var i = 0; i < files.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var file = files[i];
				var ticks = await Task.Run(() => ZTicksFileReader.Read(file), cancellationToken).ConfigureAwait(false);
				parsed += ticks.Count;

				if (ticks.Count > 0)
				{
					minUtc = minUtc is null || ticks[0].TimestampUtc < minUtc ? ticks[0].TimestampUtc : minUtc;
					maxUtc = maxUtc is null || ticks[^1].TimestampUtc > maxUtc ? ticks[^1].TimestampUtc : maxUtc;
					written += _ingest.InsertTicks(table, ticks.Select(t => new Tick(t.TimestampUtc, t.Bid, t.Ask)).ToList());
				}

				onProgress?.Invoke(new ZTicksImportProgress(i + 1, files.Count, file, parsed, written));
			}

			if (minUtc is not null && maxUtc is not null)
			{
				_ingest.LogBackfill(
					table,
					request.SymbolCode,
					request.SymbolName,
					minUtc.Value,
					maxUtc.Value,
					written,
					BackfillTrigger.User
				);
			}

			_logger.LogInformation(
				"ZTicks import finished: {Table}, {Files} file(s), {Parsed} tick(s) parsed, {Written} row(s) written.",
				table,
				files.Count,
				parsed,
				written
			);

			return new ZTicksImportResult(files.Count, parsed, written, table);
		}
	}
}
