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
		// Kept modest: a Docker-Desktop bind-mounted data directory can flake under heavy concurrent COPY traffic.
		private const int MaxParallelFiles = 4;

		private readonly ITickIngest _ingest;
		private readonly ILogger<ZTicksImportService> _logger;
		private readonly object _startLock = new();

		private volatile bool _isRunning;
		private CancellationTokenSource? _cts;

		public ZTicksImportService(ITickIngest ingest, ILogger<ZTicksImportService> logger)
		{
			_ingest = ingest;
			_logger = logger;
		}

		public bool IsRunning => _isRunning;
		public ZTicksImportProgress? Progress { get; private set; }
		public ZTicksImportResult? LastResult { get; private set; }
		public string? LastError { get; private set; }
		public bool WasCancelled { get; private set; }

		public event Action? Changed;

		public async Task<IReadOnlyList<ZTicksPreviewEntry>> PreviewAsync(string cachePath)
		{
			var files = ZTicksCacheScanner.FindFiles(cachePath, from: null, to: null);
			var entries = new ZTicksPreviewEntry[files.Count];

			await Parallel
				.ForEachAsync(
					Enumerable.Range(0, files.Count),
					new ParallelOptions { MaxDegreeOfParallelism = DegreeOfParallelism(files.Count) },
					(i, _) =>
					{
						var ticks = ZTicksFileReader.Read(files[i]);
						entries[i] = new ZTicksPreviewEntry(
							files[i],
							ZTicksCacheScanner.TryParseFileDate(files[i]),
							ticks.Count,
							ticks.Count > 0 ? ticks[0].TimestampUtc : null,
							ticks.Count > 0 ? ticks[^1].TimestampUtc : null
						);
						return ValueTask.CompletedTask;
					}
				)
				.ConfigureAwait(false);

			return entries;
		}

		public bool StartImport(ZTicksImportRequest request)
		{
			CancellationTokenSource cts;
			lock (_startLock)
			{
				if (_isRunning)
					return false;

				_isRunning = true;
				Progress = null;
				LastResult = null;
				LastError = null;
				WasCancelled = false;
				cts = _cts = new CancellationTokenSource();
			}

			Changed?.Invoke();
			_ = RunAsync(request, cts);
			return true;
		}

		public void CancelImport() => _cts?.Cancel();

		private async Task RunAsync(ZTicksImportRequest request, CancellationTokenSource cts)
		{
			try
			{
				LastResult = await ImportAsync(request, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				LastError = "Import cancelled.";
				WasCancelled = true;
				_logger.LogWarning("ZTicks import cancelled for {Symbol}.", request.SymbolName);
			}
			catch (Exception ex)
			{
				LastError = ex.Message;
				_logger.LogError(ex, "ZTicks import failed for {Symbol}.", request.SymbolName);
			}
			finally
			{
				_isRunning = false;
				cts.Dispose();
				Changed?.Invoke();
			}
		}

		private async Task<ZTicksImportResult> ImportAsync(ZTicksImportRequest request, CancellationToken cancellationToken)
		{
			var files = ZTicksCacheScanner.FindFiles(request.CachePath, from: null, to: null);
			if (request.OnlyDates is { } onlyDates)
				files = files.Where(f => ZTicksCacheScanner.TryParseFileDate(f) is { } d && onlyDates.Contains(d)).ToList();
			var table = TickTableNaming.ForSymbol(request.SymbolName, isLive: true);

			if (!_ingest.TableExists(table))
			{
				if (!request.CreateTableIfMissing)
					throw new InvalidOperationException($"Table {table} does not exist and creating it was not requested.");
				_ingest.CreateTable(table);
			}

			_ingest.AssociateSymbol(table, request.SymbolName);
			_ingest.SetSourcePath(table, request.CachePath);

			long parsed = 0;
			long written = 0;
			long filesDone = 0;
			DateTime? minUtc = null;
			DateTime? maxUtc = null;
			var boundsLock = new object();
			var degreeOfParallelism = DegreeOfParallelism(files.Count);

			// Each file is a self-contained day, so files parse and insert concurrently; only the running min/max needs a lock.
			await Parallel
				.ForEachAsync(
					files,
					new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism, CancellationToken = cancellationToken },
					(file, _) =>
					{
						var ticks = ZTicksFileReader.Read(file);
						Interlocked.Add(ref parsed, ticks.Count);

						var fileMinUtc = ticks.Count > 0 ? ticks[0].TimestampUtc : (DateTime?)null;
						var fileMaxUtc = ticks.Count > 0 ? ticks[^1].TimestampUtc : (DateTime?)null;

						if (ticks.Count > 0)
						{
							var fileWritten = _ingest.InsertTicks(
								table,
								ticks.Select(t => new Tick(t.TimestampUtc, t.Bid, t.Ask)).ToList()
							);
							Interlocked.Add(ref written, fileWritten);

							lock (boundsLock)
							{
								if (minUtc is null || fileMinUtc < minUtc)
									minUtc = fileMinUtc;
								if (maxUtc is null || fileMaxUtc > maxUtc)
									maxUtc = fileMaxUtc;
							}
						}

						if (ZTicksCacheScanner.TryParseFileDate(file) is { } day)
							_ingest.UpsertDailyStat(table, day, ticks.Count, fileMinUtc, fileMaxUtc);

						var done = Interlocked.Increment(ref filesDone);
						Progress = new ZTicksImportProgress((int)done, files.Count, file, Interlocked.Read(ref parsed), Interlocked.Read(ref written));
						Changed?.Invoke();
						return ValueTask.CompletedTask;
					}
				)
				.ConfigureAwait(false);

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
				"ZTicks import finished: {Table}, {Files} file(s), {Parsed} tick(s) parsed, {Written} row(s) written, parallelism={Parallelism}.",
				table,
				files.Count,
				parsed,
				written,
				degreeOfParallelism
			);

			return new ZTicksImportResult(files.Count, parsed, written, table);
		}

		private static int DegreeOfParallelism(int fileCount) => Math.Max(1, Math.Min(MaxParallelFiles, Math.Min(Environment.ProcessorCount, fileCount)));
	}
}
