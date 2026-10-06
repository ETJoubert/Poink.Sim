using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Scoped per circuit, so the Trade page and a popped-out chart each hold their own series. Ticks that arrive while a
	/// symbol's history is still loading are not applied, since the aggregator must see ticks in time order. Series belong
	/// to the account they were loaded on: a switch (e.g. demo to live, where symbol ids repeat but prices diverge) drops
	/// them all, and a load that finishes after a switch is discarded rather than held.
	/// </summary>
	internal sealed class TradeBarFeed : ITradeBarFeed, IDisposable
	{
		/// <summary>Bar count a fresh load targets, bounding how much raw tick history is read per aggregation.</summary>
		private const int TargetHistoryBarCount = 300;

		/// <summary>Ceiling on the history window regardless of bar size, so a coarse aggregation can't read weeks of ticks.</summary>
		private static readonly TimeSpan MaxHistoryLookback = TimeSpan.FromDays(14);

		private readonly ITickStreamAccess _stream;
		private readonly IAccountAccess _account;
		private readonly ITickHistoryService _tickHistory;
		private readonly ConcurrentDictionary<long, TradeSymbolSeries> _series = new();
		private readonly ConcurrentDictionary<(long SymbolId, int BarSizeSeconds), Task<TradeSymbolSeries>> _loads = new();

		private long? _accountId;

		public TradeBarFeed(ITickStreamAccess stream, IAccountAccess account, ITickHistoryService tickHistory)
		{
			_stream = stream;
			_account = account;
			_tickHistory = tickHistory;
			_accountId = account.SelectedAccountId;
			_stream.TickReceived += OnTickReceived;
			_account.Changed += OnAccountChanged;
		}

		public event Action<long>? SeriesChanged;

		public event Action? Reset;

		public TradeSymbolSeries? Get(long symbolId) => _series.TryGetValue(symbolId, out var series) ? series : null;

		public TradeSymbolSeries? GetByTable(string table) => _series.Values.FirstOrDefault(s => s.History.TodayTable == table);

		public bool IsLoading(long symbolId) => _loads.Keys.Any(k => k.SymbolId == symbolId);

		public Task<TradeSymbolSeries> LoadAsync(long symbolId, string symbolName, int barSizeSeconds, double pipSize)
		{
			if (Get(symbolId) is { } held && held.BarSizeSeconds == barSizeSeconds)
				return Task.FromResult(held);

			var accountId = _account.SelectedAccountId;
			return _loads.GetOrAdd((symbolId, barSizeSeconds), key => LoadFreshAsync(key, symbolName, pipSize, accountId));
		}

		public void Invalidate(long symbolId) => _series.TryRemove(symbolId, out _);

		public void RetainOnly(IReadOnlySet<long> symbolIds)
		{
			foreach (var symbolId in _series.Keys.Where(id => !symbolIds.Contains(id)))
				_series.TryRemove(symbolId, out _);
		}

		public static TimeSpan LookbackFor(int barSizeSeconds) =>
			TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(barSizeSeconds).Ticks * TargetHistoryBarCount, MaxHistoryLookback.Ticks));

		private async Task<TradeSymbolSeries> LoadFreshAsync(
			(long SymbolId, int BarSizeSeconds) key,
			string symbolName,
			double pipSize,
			long? accountId
		)
		{
			// Yields first so the load is registered in _loads before its finally can remove it, even when history returns synchronously.
			await Task.Yield();
			try
			{
				_series.TryRemove(key.SymbolId, out _);
				var to = DateTime.UtcNow;
				var history = await _tickHistory.LoadAsync(
					new TickHistoryRequest(key.SymbolId, symbolName, to - LookbackFor(key.BarSizeSeconds), to, BackfillTrigger.User)
				);
				var series = TradeSymbolSeries.FromHistory(key.SymbolId, symbolName, key.BarSizeSeconds, pipSize, history);
				if (_account.SelectedAccountId != accountId)
					return series;

				_series[key.SymbolId] = series;
				SeriesChanged?.Invoke(key.SymbolId);
				return series;
			}
			finally
			{
				_loads.TryRemove(key, out _);
			}
		}

		private void OnTickReceived(MarketTick tick)
		{
			if (!_series.TryGetValue(tick.SymbolId, out var series))
				return;

			series.Apply(new Tick(tick.TimestampUtc, tick.Bid, tick.Ask));
			SeriesChanged?.Invoke(tick.SymbolId);
		}

		private void OnAccountChanged()
		{
			var accountId = _account.SelectedAccountId;
			if (accountId == _accountId)
				return;

			_accountId = accountId;
			_series.Clear();
			if (accountId is not null)
				Reset?.Invoke();
		}

		public void Dispose()
		{
			_stream.TickReceived -= OnTickReceived;
			_account.Changed -= OnAccountChanged;
		}
	}
}
