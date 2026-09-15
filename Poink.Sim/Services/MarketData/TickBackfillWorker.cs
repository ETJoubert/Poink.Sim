using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>
	/// Single writer of the tick store. Buffers live ticks per tracked symbol and, once the DB is
	/// contiguous up to the stream's start, drains that buffer forward. The pre-stream gap is filled from
	/// <see cref="IHistoricalTickSource"/> (unavailable until cTrader ProtoOAGetTickData is wired in), so
	/// today only empty or already-current tables advance past ClosingGap.
	/// </summary>
	internal sealed class TickBackfillWorker : BackgroundService, ITickBackfillController
	{
		private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan GapRetryInterval = TimeSpan.FromSeconds(5);
		private const int MaxBufferTicks = 500_000;
		private const int BufferTrimChunk = 4096;

		private readonly ITickIngest _ingest;
		private readonly IHistoricalTickSource _history;
		private readonly ICTraderSessionService _session;
		private readonly ILogger<TickBackfillWorker> _logger;

		private readonly ConcurrentDictionary<long, Job> _jobs = new();
		private readonly SemaphoreSlim _wake = new(0);
		private volatile bool _paused;

		public TickBackfillWorker(
			ITickIngest ingest,
			IHistoricalTickSource history,
			ICTraderSessionService session,
			ILogger<TickBackfillWorker> logger
		)
		{
			_ingest = ingest;
			_history = history;
			_session = session;
			_logger = logger;
			_session.TickReceived += OnTick;
		}

		public bool IsPaused => _paused;

		public event Action? Changed;

		public void Pause()
		{
			_paused = true;
			_logger.LogInformation("Tick backfill worker paused.");
			Changed?.Invoke();
		}

		public void Resume()
		{
			_paused = false;
			_logger.LogInformation("Tick backfill worker resumed.");
			Signal();
			Changed?.Invoke();
		}

		public void Track(long symbolId, string symbolName, bool createTable)
		{
			var table = _ingest.GetTableForSymbol(symbolName) ?? TickTableNaming.ForSymbol(symbolName, _session.UseLiveEnvironment);
			var exists = _ingest.TableExists(table);
			if (!exists && createTable)
			{
				_ingest.CreateTable(table);
				exists = true;
			}

			if (exists)
				_ingest.AssociateSymbol(table, symbolName);

			var coverage = exists ? _ingest.GetCoverage(table) : new TickCoverage(false, 0, null, null);
			var job = new Job(symbolId, symbolName, table)
			{
				TableExists = exists,
				RowsInDb = coverage.Rows,
				MinUtc = coverage.MinUtc,
				MaxUtc = coverage.MaxUtc,
				State = exists ? TickIngestState.Idle : TickIngestState.NoTable,
			};
			_jobs[symbolId] = job;

			_logger.LogInformation(
				"Tracking {Symbol} -> {Table} (exists={Exists}, rows={Rows}, max={Max}).",
				symbolName,
				table,
				exists,
				coverage.Rows,
				coverage.MaxUtc
			);

			Signal();
			Changed?.Invoke();
		}

		public void Untrack(long symbolId)
		{
			if (_jobs.TryRemove(symbolId, out var job))
			{
				_logger.LogInformation("Stopped tracking {Symbol} ({Table}).", job.SymbolName, job.Table);
				Changed?.Invoke();
			}
		}

		public void ForceFlush(long symbolId) => Signal();

		public async Task<long> BackfillRangeAsync(long symbolId, string symbolName, DateTime fromUtc, DateTime toUtc)
		{
			if (!_history.IsAvailable)
			{
				_logger.LogWarning(
					"Manual backfill for {Symbol} skipped: history source unavailable (connect a session).",
					symbolName
				);
				return 0;
			}

			var table = _ingest.GetTableForSymbol(symbolName) ?? TickTableNaming.ForSymbol(symbolName, _session.UseLiveEnvironment);
			if (!_ingest.TableExists(table))
			{
				_logger.LogWarning(
					"Manual backfill for {Symbol} skipped: table {Table} does not exist.",
					symbolName,
					table
				);
				return 0;
			}

			_ingest.AssociateSymbol(table, symbolName);

			var ticks = await _history.GetTicksAsync(symbolId, fromUtc, toUtc);
			if (ticks.Count == 0)
			{
				_logger.LogInformation(
					"Manual backfill for {Table} returned no ticks ({From:u}..{To:u}).",
					table,
					fromUtc,
					toUtc
				);
				_ingest.LogBackfill(table, symbolId, symbolName, fromUtc, toUtc, 0, BackfillTrigger.User);
				return 0;
			}

			var written = _ingest.InsertTicks(table, ticks);
			_logger.LogInformation(
				"Manual backfill wrote {Count} tick(s) to {Table} ({From:u}..{To:u}).",
				written,
				table,
				fromUtc,
				toUtc
			);
			_ingest.LogBackfill(table, symbolId, symbolName, fromUtc, toUtc, written, BackfillTrigger.User);

			if (_jobs.TryGetValue(symbolId, out var job))
			{
				var coverage = _ingest.GetCoverage(table);
				job.RowsInDb = coverage.Rows;
				job.MinUtc = coverage.MinUtc;
				job.MaxUtc = coverage.MaxUtc;
			}

			Changed?.Invoke();
			return written;
		}

		public IReadOnlyList<SymbolIngestStatus> GetStatuses()
		{
			var now = DateTime.UtcNow;
			return _jobs
				.Values.OrderBy(j => j.SymbolName)
				.Select(j =>
				{
					int buffered;
					lock (j.Lock)
						buffered = j.Buffer.Count;
					var lag = j.MaxUtc is { } max ? (now - max).TotalSeconds : (double?)null;
					return new SymbolIngestStatus(
						j.SymbolId,
						j.SymbolName,
						j.Table,
						j.TableExists,
						j.State,
						j.RowsInDb,
						j.MinUtc,
						j.MaxUtc,
						buffered,
						j.WrittenThisSession,
						lag,
						j.LastError
					);
				})
				.ToList();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				if (!_paused)
				{
					var changed = false;
					foreach (var job in _jobs.Values)
					{
						try
						{
							changed |= await ProcessJobAsync(job, stoppingToken);
						}
						catch (Exception ex)
						{
							job.State = TickIngestState.Error;
							job.LastError = ex.Message;
							_logger.LogError(ex, "Tick ingest failed for {Table}.", job.Table);
							changed = true;
						}
					}

					if (changed)
						Changed?.Invoke();
				}

				try
				{
					await _wake.WaitAsync(FlushInterval, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		private async Task<bool> ProcessJobAsync(Job job, CancellationToken cancellationToken)
		{
			if (!job.TableExists)
			{
				job.State = TickIngestState.NoTable;
				return false;
			}

			DateTime? streamStart;
			lock (job.Lock)
				streamStart = job.StreamStart;

			if (streamStart is null)
			{
				job.State = TickIngestState.Idle;
				return false;
			}

			var gapClosed = job.MaxUtc is null || job.MaxUtc.Value >= streamStart.Value;
			if (!gapClosed)
			{
				job.State = TickIngestState.ClosingGap;
				if (!_history.IsAvailable)
				{
					if (!job.GapWarned)
					{
						_logger.LogWarning(
							"Cannot backfill {Table}: tick history source unavailable. Gap {From}..{To} awaits a connected session.",
							job.Table,
							job.MaxUtc,
							streamStart
						);
						job.GapWarned = true;
					}
					return false;
				}

				if (DateTime.UtcNow < job.NextGapAttemptUtc)
					return false;
				job.NextGapAttemptUtc = DateTime.UtcNow.Add(GapRetryInterval);

				var fetched = await _history.GetTicksAsync(
					job.SymbolId,
					job.MaxUtc!.Value,
					streamStart.Value,
					cancellationToken
				);
				var cutoff = job.MaxUtc ?? DateTime.MinValue;
				var gapTicks = fetched.Where(t => t.Timestamp > cutoff).ToList();
				if (gapTicks.Count == 0)
					return false;

				var gapWritten = _ingest.InsertTicks(job.Table, gapTicks);
				_ingest.LogBackfill(
					job.Table,
					job.SymbolId,
					job.SymbolName,
					cutoff,
					streamStart.Value,
					gapWritten,
					BackfillTrigger.Worker
				);
				job.RowsInDb += gapWritten;
				job.WrittenThisSession += gapWritten;
				job.MaxUtc = gapTicks[^1].Timestamp;
				return true;
			}

			List<Tick> pending;
			lock (job.Lock)
			{
				var cutoff = job.MaxUtc ?? DateTime.MinValue;
				pending = job.Buffer.Where(t => t.Timestamp > cutoff).OrderBy(t => t.Timestamp).ToList();
			}

			if (pending.Count == 0)
			{
				job.State = TickIngestState.TailingLive;
				return false;
			}

			var written = _ingest.InsertTicks(job.Table, pending);
			var lastWritten = pending[^1].Timestamp;

			lock (job.Lock)
				job.Buffer.RemoveAll(t => t.Timestamp <= lastWritten);

			job.RowsInDb += written;
			job.WrittenThisSession += written;
			job.MinUtc ??= pending[0].Timestamp;
			job.MaxUtc = lastWritten;
			job.State = TickIngestState.TailingLive;
			job.LastError = null;
			return true;
		}

		private void OnTick(MarketTick tick)
		{
			if (!_jobs.TryGetValue(tick.SymbolId, out var job) || !job.TableExists)
				return;

			lock (job.Lock)
			{
				job.Buffer.Add(new Tick(tick.TimestampUtc, tick.Bid, tick.Ask));
				job.StreamStart ??= tick.TimestampUtc;

				if (job.Buffer.Count > MaxBufferTicks)
				{
					job.Buffer.RemoveRange(0, BufferTrimChunk);
					job.StreamStart = job.Buffer[0].Timestamp;
				}
			}
		}

		private void Signal()
		{
			try
			{
				_wake.Release();
			}
			catch (SemaphoreFullException) { }
		}

		public override void Dispose()
		{
			_session.TickReceived -= OnTick;
			_wake.Dispose();
			base.Dispose();
		}

		private sealed class Job
		{
			public Job(long symbolId, string symbolName, string table)
			{
				SymbolId = symbolId;
				SymbolName = symbolName;
				Table = table;
			}

			public long SymbolId { get; }
			public string SymbolName { get; }
			public string Table { get; }

			public readonly object Lock = new();
			public readonly List<Tick> Buffer = new();

			public DateTime? StreamStart;
			public bool TableExists;
			public TickIngestState State;
			public long RowsInDb;
			public DateTime? MinUtc;
			public DateTime? MaxUtc;
			public long WrittenThisSession;
			public string? LastError;
			public bool GapWarned;
			public DateTime NextGapAttemptUtc;
		}
	}
}
