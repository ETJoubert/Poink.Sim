using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>
	/// History before today always reads the live table, since the Data page import only writes live tables; today's
	/// ticks read and write the connected environment's table, so a demo account keeps its own diverging prices.
	/// </summary>
	internal sealed class TickHistoryService : ITickHistoryService
	{
		private readonly ITickRepository _ticks;
		private readonly ITickIngest _ingest;
		private readonly ICTraderHistory _history;
		private readonly ILogger<TickHistoryService> _logger;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _tableGates = new();

		public TickHistoryService(
			ITickRepository ticks,
			ITickIngest ingest,
			ICTraderHistory history,
			ILogger<TickHistoryService> logger
		)
		{
			_ticks = ticks;
			_ingest = ingest;
			_history = history;
			_logger = logger;
		}

		public async Task<TickHistoryResult> LoadAsync(TickHistoryRequest request, CancellationToken cancellationToken = default)
		{
			var historyTable = TickTableNaming.ForSymbol(request.SymbolName, isLive: true);
			var todayTable = TodayTableFor(request.SymbolName);

			var gate = GateFor(todayTable);
			await gate.WaitAsync(cancellationToken);
			try
			{
				var plan = await Task.Run(
					() => TickHistoryPlanner.Plan(request.FromUtc, request.ToUtc, LastStoredToday(todayTable, request.ToUtc)),
					cancellationToken
				);

				var historyTicks = plan.HasHistory
					? await Task.Run(() => ReadStored(historyTable, plan.HistoryFromUtc, plan.HistoryToUtc), cancellationToken)
					: new List<Tick>();
				var storedToday = plan.HasStoredToday
					? await Task.Run(() => ReadStored(todayTable, plan.TodayFromUtc, plan.ApiFromUtc), cancellationToken)
					: new List<Tick>();

				var apiUnavailable = plan.HasApi && !_history.CanFetchHistory;
				var apiTicks = plan.HasApi && !apiUnavailable
					? await FetchAndStoreAsync(request.SymbolId, request.SymbolName, todayTable, plan.ApiFromUtc, plan.ToUtc, request.Trigger, cancellationToken)
					: new List<Tick>();

				var missingDays = TickHistoryPlanner.MissingWeekdays(plan.HistoryFromUtc, plan.HistoryToUtc, historyTicks);
				var result = new TickHistoryResult(
					[.. historyTicks, .. storedToday, .. apiTicks],
					historyTable,
					todayTable,
					historyTicks.Count + storedToday.Count,
					apiTicks.Count,
					apiUnavailable,
					missingDays
				);

				LogLoad(request, result);
				return result;
			}
			finally
			{
				gate.Release();
			}
		}

		public async Task<int> CatchUpTodayAsync(
			long symbolId,
			string symbolName,
			DateTime upToUtc,
			BackfillTrigger trigger,
			CancellationToken cancellationToken = default
		)
		{
			if (!_history.CanFetchHistory)
				return 0;

			var todayTable = TodayTableFor(symbolName);
			var gate = GateFor(todayTable);
			await gate.WaitAsync(cancellationToken);
			try
			{
				var plan = await Task.Run(
					() => TickHistoryPlanner.Plan(TickHistoryPlanner.DayStartUtc(upToUtc), upToUtc, LastStoredToday(todayTable, upToUtc)),
					cancellationToken
				);
				if (!plan.HasApi)
					return 0;

				var fetched = await FetchAndStoreAsync(symbolId, symbolName, todayTable, plan.ApiFromUtc, plan.ToUtc, trigger, cancellationToken);
				return fetched.Count;
			}
			finally
			{
				gate.Release();
			}
		}

		private string TodayTableFor(string symbolName) => TickTableNaming.ForSymbol(symbolName, _history.UseLiveEnvironment);

		private SemaphoreSlim GateFor(string table) => _tableGates.GetOrAdd(table, _ => new SemaphoreSlim(1, 1));

		private DateTime? LastStoredToday(string table, DateTime toUtc)
		{
			if (!_ingest.TableExists(table))
				return null;

			var recent = _ticks.ReadRecentTicks(table, toUtc, 1);
			return recent.Count > 0 && recent[0].Timestamp >= TickHistoryPlanner.DayStartUtc(toUtc) ? recent[0].Timestamp : null;
		}

		private List<Tick> ReadStored(string table, DateTime fromUtc, DateTime toUtc) =>
			_ingest.TableExists(table) ? _ticks.ReadTicks(table, fromUtc, toUtc).ToList() : new List<Tick>();

		private async Task<List<Tick>> FetchAndStoreAsync(
			long symbolId,
			string symbolName,
			string table,
			DateTime fromUtc,
			DateTime toUtc,
			BackfillTrigger trigger,
			CancellationToken cancellationToken
		)
		{
			var accountId = _history.SelectedAccountId;
			var fetched = await _history.GetHistoricalTicksAsync(symbolId, fromUtc, toUtc, cancellationToken);
			var ticks = fetched.Where(t => t.Timestamp >= fromUtc && t.Timestamp < toUtc).OrderBy(t => t.Timestamp).ToList();
			if (ticks.Count == 0)
				return ticks;

			if (_history.SelectedAccountId != accountId)
			{
				_logger.LogWarning(
					"Discarded {Count} API tick(s) for {Symbol}: the account switched during the fetch, so they may not belong in {Table}.",
					ticks.Count,
					symbolName,
					table
				);
				return new List<Tick>();
			}

			try
			{
				await Task.Run(() => Store(table, symbolId, symbolName, fromUtc, toUtc, ticks, trigger), cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogError(ex, "Failed to commit {Count} API tick(s) for {Symbol} to {Table}; they are used for this load only.", ticks.Count, symbolName, table);
			}

			return ticks;
		}

		private void Store(
			string table,
			long symbolId,
			string symbolName,
			DateTime fromUtc,
			DateTime toUtc,
			IReadOnlyList<Tick> ticks,
			BackfillTrigger trigger
		)
		{
			if (!_ingest.TableExists(table))
			{
				_ingest.CreateTable(table);
				_ingest.AssociateSymbol(table, symbolName);
			}

			var written = _ingest.InsertTicks(table, ticks);
			foreach (var day in TickHistoryPlanner.DaysSpanned(ticks))
				_ingest.RefreshDailyStat(table, day);

			_ingest.LogBackfill(table, symbolId, symbolName, fromUtc, toUtc, written, trigger);
		}

		private void LogLoad(TickHistoryRequest request, TickHistoryResult result)
		{
			_logger.LogInformation(
				"Loaded tick history for {Symbol} {From:u}..{To:u}: {DatabaseCount} tick(s) from {HistoryTable}/{TodayTable}, {ApiCount} from the API.",
				request.SymbolName,
				request.FromUtc,
				request.ToUtc,
				result.DatabaseTickCount,
				result.HistoryTable,
				result.TodayTable,
				result.ApiTickCount
			);

			if (result.ApiUnavailable)
				_logger.LogWarning("Tick history for {Symbol}: API unavailable, today's unstored ticks were skipped.", request.SymbolName);

			if (result.MissingDays.Count > 0)
				_logger.LogWarning(
					"Tick history for {Symbol}: {Table} has no ticks for {Days}; import them from the Data page.",
					request.SymbolName,
					result.HistoryTable,
					string.Join(", ", result.MissingDays.Select(d => d.ToString("yyyy-MM-dd")))
				);
		}
	}
}
