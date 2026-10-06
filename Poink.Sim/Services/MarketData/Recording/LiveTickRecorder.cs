using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData.History;

namespace Poink.Sim.Services.MarketData.Recording
{
	/// <summary>
	/// A table's live ticks are held until today's API catch-up has filled the database up to the first of them, so
	/// stored ticks for today stay contiguous and the next history load only needs to fetch past the latest one.
	/// </summary>
	internal sealed class LiveTickRecorder : ILiveTickRecorder
	{
		private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan DailyStatInterval = TimeSpan.FromMinutes(1);
		private static readonly TimeSpan CatchUpRetryDelay = TimeSpan.FromMinutes(1);

		private readonly ITickStreamAccess _stream;
		private readonly ICTraderHistory _history;
		private readonly ITickHistoryService _tickHistory;
		private readonly ITickIngest _ingest;
		private readonly ILogger<LiveTickRecorder> _logger;

		private readonly Lock _leaseLock = new();
		private readonly SemaphoreSlim _flushGate = new(1, 1);
		private readonly LiveTickBuffer _buffer = new();
		private readonly ConcurrentDictionary<string, CatchUpState> _catchUps = new();
		private readonly HashSet<string> _ensuredTables = new();
		private readonly Dictionary<string, DateTime> _dailyStatRefreshedUtc = new();

		private int _leaseCount;
		private CancellationTokenSource? _runCts;
		private Task? _flushLoop;
		private long _writtenThisRun;

		public LiveTickRecorder(
			ITickStreamAccess stream,
			ICTraderHistory history,
			ITickHistoryService tickHistory,
			ITickIngest ingest,
			ILogger<LiveTickRecorder> logger
		)
		{
			_stream = stream;
			_history = history;
			_tickHistory = tickHistory;
			_ingest = ingest;
			_logger = logger;
		}

		public bool IsRecording
		{
			get
			{
				lock (_leaseLock)
					return _leaseCount > 0;
			}
		}

		public IAsyncDisposable Acquire()
		{
			lock (_leaseLock)
			{
				if (++_leaseCount == 1)
					Start();
			}
			return new Lease(this);
		}

		private void Start()
		{
			_catchUps.Clear();
			Interlocked.Exchange(ref _writtenThisRun, 0);
			_runCts = new CancellationTokenSource();
			_stream.TickReceived += OnTickReceived;
			_flushLoop = Task.Run(() => FlushLoopAsync(_runCts.Token));
			_logger.LogInformation("Live tick recording started; flushing every {Interval}.", FlushInterval);
		}

		private async ValueTask ReleaseAsync()
		{
			CancellationTokenSource? cts;
			Task? loop;
			lock (_leaseLock)
			{
				if (_leaseCount == 0 || --_leaseCount > 0)
					return;

				_stream.TickReceived -= OnTickReceived;
				cts = _runCts;
				loop = _flushLoop;
				_runCts = null;
				_flushLoop = null;
			}

			cts?.Cancel();
			if (loop is not null)
				await loop;
			cts?.Dispose();

			await FlushAsync(isFinal: true, CancellationToken.None);
			_logger.LogInformation("Live tick recording stopped; {Count} tick(s) written this run.", Interlocked.Read(ref _writtenThisRun));
		}

		private void OnTickReceived(MarketTick tick)
		{
			var table = TickTableNaming.ForSymbol(tick.SymbolName, _history.UseLiveEnvironment);
			_buffer.Add(table, tick.SymbolId, tick.SymbolName, new Tick(tick.TimestampUtc, tick.Bid, tick.Ask));
		}

		private async Task FlushLoopAsync(CancellationToken cancellationToken)
		{
			using var timer = new PeriodicTimer(FlushInterval);
			try
			{
				while (await timer.WaitForNextTickAsync(cancellationToken))
					await FlushAsync(isFinal: false, cancellationToken);
			}
			catch (OperationCanceledException) { }
		}

		private async Task FlushAsync(bool isFinal, CancellationToken cancellationToken)
		{
			await _flushGate.WaitAsync(CancellationToken.None);
			try
			{
				foreach (var pending in _buffer.PendingTables())
				{
					var state = _catchUps.GetValueOrDefault(pending.Table);

					if (state.Status == CatchUpStatus.Done)
						await WriteAsync(pending.Table, isFinal);
					else if (isFinal)
						DiscardUncaughtUp(pending.Table);
					else if (state.Status != CatchUpStatus.Running && DateTime.UtcNow >= state.RetryAfterUtc)
						StartCatchUp(pending, cancellationToken);
				}
			}
			finally
			{
				_flushGate.Release();
			}
		}

		private void StartCatchUp(PendingLiveTable pending, CancellationToken cancellationToken)
		{
			_catchUps[pending.Table] = new CatchUpState(CatchUpStatus.Running, DateTime.MinValue);
			_ = Task.Run(async () =>
			{
				try
				{
					await _tickHistory.CatchUpTodayAsync(pending.SymbolId, pending.SymbolName, pending.EarliestUtc, BackfillTrigger.Worker, cancellationToken);
					_catchUps[pending.Table] = new CatchUpState(CatchUpStatus.Done, DateTime.MinValue);
				}
				catch (OperationCanceledException)
				{
					_catchUps[pending.Table] = default;
				}
				catch (Exception ex)
				{
					_catchUps[pending.Table] = new CatchUpState(CatchUpStatus.Failed, DateTime.UtcNow + CatchUpRetryDelay);
					_logger.LogWarning(ex, "Today's tick catch-up for {Table} failed; holding its live ticks and retrying in {Delay}.", pending.Table, CatchUpRetryDelay);
				}
			});
		}

		private void DiscardUncaughtUp(string table)
		{
			var dropped = _buffer.Discard(table);
			_logger.LogWarning(
				"Dropped {Count} live tick(s) for {Table}: today's catch-up had not finished, and writing them would leave a gap the next load can't see.",
				dropped,
				table
			);
		}

		private async Task WriteAsync(string table, bool isFinal)
		{
			if (_buffer.Drain(table) is not { } batch || batch.Ticks.Count == 0)
				return;

			try
			{
				await Task.Run(() => Write(batch, isFinal));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to write {Count} live tick(s) to {Table}.", batch.Ticks.Count, table);
			}
		}

		private void Write(LiveTickBatch batch, bool isFinal)
		{
			if (!_ensuredTables.Contains(batch.Table))
			{
				if (!_ingest.TableExists(batch.Table))
				{
					_ingest.CreateTable(batch.Table);
					_ingest.AssociateSymbol(batch.Table, batch.SymbolName);
				}
				_ensuredTables.Add(batch.Table);
			}

			Interlocked.Add(ref _writtenThisRun, _ingest.InsertTicks(batch.Table, batch.Ticks));

			var now = DateTime.UtcNow;
			if (!isFinal && _dailyStatRefreshedUtc.GetValueOrDefault(batch.Table) + DailyStatInterval > now)
				return;

			foreach (var day in TickHistoryPlanner.DaysSpanned(batch.Ticks))
				_ingest.RefreshDailyStat(batch.Table, day);
			_dailyStatRefreshedUtc[batch.Table] = now;
		}

		private enum CatchUpStatus
		{
			NotStarted,
			Running,
			Done,
			Failed,
		}

		private readonly record struct CatchUpState(CatchUpStatus Status, DateTime RetryAfterUtc);

		private sealed class Lease(LiveTickRecorder recorder) : IAsyncDisposable
		{
			private int _disposed;

			public ValueTask DisposeAsync() =>
				Interlocked.Exchange(ref _disposed, 1) == 0 ? recorder.ReleaseAsync() : ValueTask.CompletedTask;
		}
	}
}
