using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData.Recording;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>
	/// Fetches before deleting, so a failed or cross-account fetch leaves today's stored ticks untouched; the delete and
	/// insert then commit together. Live ticks still buffered from before the fetch's end are dropped, since writing them
	/// afterwards would mix recorded quotes back into the freshly fetched day.
	/// </summary>
	internal sealed class TodayTickReloadService : ITodayTickReloadService
	{
		private readonly ICTraderHistory _history;
		private readonly ITickIngest _ingest;
		private readonly ILiveTickRecorder _recorder;
		private readonly ILogger<TodayTickReloadService> _logger;

		public TodayTickReloadService(
			ICTraderHistory history,
			ITickIngest ingest,
			ILiveTickRecorder recorder,
			ILogger<TodayTickReloadService> logger
		)
		{
			_history = history;
			_ingest = ingest;
			_recorder = recorder;
			_logger = logger;
		}

		public string TableFor(string symbolName) => TickTableNaming.ForSymbol(symbolName, _history.UseLiveEnvironment);

		public async Task<TodayTickReloadResult> ReloadAsync(long symbolId, string symbolName, CancellationToken cancellationToken = default)
		{
			var table = TableFor(symbolName);
			if (!_history.CanFetchHistory)
				return new TodayTickReloadResult(table, 0, 0, "cTrader history is unavailable -- connect an account first.");

			var toUtc = DateTime.UtcNow;
			var fromUtc = TickHistoryPlanner.DayStartUtc(toUtc);
			var accountId = _history.SelectedAccountId;
			_logger.LogInformation("Reloading today's ticks for {Symbol} into {Table} ({From:u}..{To:u}).", symbolName, table, fromUtc, toUtc);

			try
			{
				var fetched = await _history.GetHistoricalTicksAsync(symbolId, fromUtc, toUtc, cancellationToken);
				if (_history.SelectedAccountId != accountId)
				{
					_logger.LogWarning("Today reload for {Symbol} abandoned: the account switched during the fetch.", symbolName);
					return new TodayTickReloadResult(table, 0, 0, "The account switched during the fetch -- nothing was replaced.");
				}

				var ticks = fetched.Where(t => t.Timestamp >= fromUtc && t.Timestamp < toUtc).OrderBy(t => t.Timestamp).ToList();
				var (deleted, inserted) = await Task.Run(
					() =>
					{
						if (!_ingest.TableExists(table))
						{
							_ingest.CreateTable(table);
							_ingest.AssociateSymbol(table, symbolName);
						}

						var replaced = _ingest.ReplaceTicks(table, fromUtc, toUtc, ticks);
						_ingest.RefreshDailyStat(table, DateOnly.FromDateTime(fromUtc));
						_ingest.LogBackfill(table, symbolId, symbolName, fromUtc, toUtc, replaced.Inserted, BackfillTrigger.User);
						return replaced;
					},
					cancellationToken
				);

				var dropped = _recorder.DiscardBuffered(table, toUtc);
				_logger.LogInformation(
					"Reloaded today's ticks for {Symbol} in {Table}: {Deleted} replaced by {Inserted}, {Dropped} buffered live tick(s) dropped.",
					symbolName,
					table,
					deleted,
					inserted,
					dropped
				);
				return new TodayTickReloadResult(table, deleted, inserted, null);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogError(ex, "Failed to reload today's ticks for {Symbol} into {Table}.", symbolName, table);
				return new TodayTickReloadResult(table, 0, 0, ex.Message);
			}
		}
	}
}
