using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Services;

public class TradeBarFeedTests
{
	private readonly FakeTickStream _stream = new();
	private readonly FakeTickHistoryService _history = new();
	private readonly FakeAccountAccess _account = new();

	private TradeBarFeed CreateFeed() => new(_stream, _account, _history);

	[Fact]
	public async Task LoadAsync_BuildsSeriesFromHistory()
	{
		_history.Ticks.Add(TestTickHelper.AtMid(5, 1.1));
		using var feed = CreateFeed();

		var series = await feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		Assert.Same(series, feed.Get(1));
		Assert.Single(series.Bars);
	}

	[Fact]
	public async Task LoadAsync_SameBarSize_ReusesHeldSeries()
	{
		using var feed = CreateFeed();
		var first = await feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		var second = await feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		Assert.Same(first, second);
		Assert.Equal(1, _history.Calls);
	}

	[Fact]
	public async Task LoadAsync_ConcurrentCalls_ShareOneHistoryLoad()
	{
		_history.Gate = new TaskCompletionSource();
		using var feed = CreateFeed();

		var a = feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		var b = feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		_history.Gate.SetResult();

		Assert.Same(await a, await b);
		Assert.Equal(1, _history.Calls);
	}

	[Fact]
	public async Task LoadAsync_NewBarSize_Reloads()
	{
		using var feed = CreateFeed();
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		var reloaded = await feed.LoadAsync(1, "EURUSD", 300, 0.0001);

		Assert.Equal(300, reloaded.BarSizeSeconds);
		Assert.Equal(2, _history.Calls);
	}

	[Fact]
	public async Task LiveTick_UpdatesHeldSeriesAndRaisesChange()
	{
		_history.Ticks.Add(TestTickHelper.AtMid(5, 1.1));
		using var feed = CreateFeed();
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		var changed = new List<long>();
		feed.SeriesChanged += changed.Add;

		_stream.Raise(new MarketTick(1, "EURUSD", TestTickHelper.Origin.AddSeconds(30), 1.3, 1.3));
		_stream.Raise(new MarketTick(2, "GBPUSD", TestTickHelper.Origin.AddSeconds(30), 1.3, 1.3));

		Assert.Equal(new long[] { 1 }, changed);
		Assert.Equal(1.3, feed.Get(1)!.HlocBars[^1].Close);
	}

	[Fact]
	public async Task RetainOnly_DropsOtherSymbols()
	{
		using var feed = CreateFeed();
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		await feed.LoadAsync(2, "GBPUSD", 60, 0.0001);

		feed.RetainOnly(new HashSet<long> { 2 });

		Assert.Null(feed.Get(1));
		Assert.NotNull(feed.Get(2));
	}

	[Fact]
	public async Task AccountSwitch_DropsHeldSeriesAndRaisesReset()
	{
		using var feed = CreateFeed();
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		var resets = 0;
		feed.Reset += () => resets++;

		_account.Select(2);

		Assert.Null(feed.Get(1));
		Assert.Equal(1, resets);
	}

	[Fact]
	public async Task AccountSwitch_MidSwitchNoAccount_DropsSeriesButWaitsToRaiseReset()
	{
		using var feed = CreateFeed();
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		var resets = 0;
		feed.Reset += () => resets++;

		_account.Select(null);
		Assert.Null(feed.Get(1));
		Assert.Equal(0, resets);

		_account.Select(2);
		Assert.Equal(1, resets);
	}

	[Fact]
	public async Task AccountSwitch_LoadFinishingAfterIt_IsNotHeld()
	{
		_history.Gate = new TaskCompletionSource();
		using var feed = CreateFeed();
		var load = feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		_account.Select(2);
		_history.Gate.SetResult();
		await load;

		Assert.Null(feed.Get(1));
	}

	[Fact]
	public async Task AccountSwitch_NextLoadFetchesFreshHistory()
	{
		using var feed = CreateFeed();
		var before = await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		_account.Select(2);

		var after = await feed.LoadAsync(1, "EURUSD", 60, 0.0001);

		Assert.NotSame(before, after);
		Assert.Equal(2, _history.Calls);
	}
}
