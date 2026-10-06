using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;

namespace Poink.Sim.Tests.TestSupport;

/// <summary>A tick stream the test raises ticks on by hand.</summary>
internal sealed class FakeTickStream : ITickStreamAccess
{
	public CTraderConnectionState State => CTraderConnectionState.Connected;

	public double? Balance => null;

	public double? Leverage => null;

	public IReadOnlyList<CTraderPositionInfo> Positions => Array.Empty<CTraderPositionInfo>();

	public IReadOnlyList<CTraderSymbolQuote> Quotes => Array.Empty<CTraderSymbolQuote>();

	public event Action<MarketTick>? TickReceived;

	public void Raise(MarketTick tick) => TickReceived?.Invoke(tick);
}

/// <summary>Returns a fixed tick list as history, optionally held until <see cref="Gate"/> completes.</summary>
internal sealed class FakeTickHistoryService : ITickHistoryService
{
	public List<Tick> Ticks { get; } = new();

	public TaskCompletionSource? Gate { get; set; }

	public int Calls { get; private set; }

	public string Table { get; set; } = "live_ticks_x";

	public async Task<TickHistoryResult> LoadAsync(TickHistoryRequest request, CancellationToken cancellationToken = default)
	{
		Calls++;
		if (Gate is not null)
			await Gate.Task;
		return new TickHistoryResult(Ticks.ToArray(), Table, Table, Ticks.Count, 0, false, Array.Empty<DateOnly>());
	}

	public Task<int> CatchUpTodayAsync(
		long symbolId,
		string symbolName,
		DateTime upToUtc,
		BackfillTrigger trigger,
		CancellationToken cancellationToken = default
	) => Task.FromResult(0);
}
