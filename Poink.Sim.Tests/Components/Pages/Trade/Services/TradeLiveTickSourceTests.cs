using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Services;

public class TradeLiveTickSourceTests
{
	private const string Table = "live_ticks_eurusd";
	private static readonly CompareTxFoldSymbol Symbol = new(Table, "--color-primary");

	private readonly InMemoryTickRepository _store = new();
	private readonly FakeTickStream _stream = new();
	private readonly FakeTickHistoryService _history = new() { Table = Table };

	private static System.DateTime At(double seconds) => TestTickHelper.Origin.AddSeconds(seconds);

	private async Task<(TradeBarFeed Feed, TradeLiveTickSource Source)> LoadedAsync()
	{
		var feed = new TradeBarFeed(_stream, _history);
		await feed.LoadAsync(1, "EURUSD", 60, 0.0001);
		return (feed, new TradeLiveTickSource(_store, feed));
	}

	[Fact]
	public async Task ReadTicks_JoinsStoredTicksBeforeHeldOnesWithLiveTicks()
	{
		_store.Seed(Table, [TestTickHelper.AtMid(10, 1), TestTickHelper.AtMid(20, 2), TestTickHelper.AtMid(100, 99)]);
		_history.Ticks.Add(TestTickHelper.AtMid(100, 3));
		var (feed, source) = await LoadedAsync();
		_stream.Raise(new MarketTick(1, "EURUSD", At(110), 4, 4));

		var ticks = source.ReadTicks(Symbol, null, At(0), At(200));

		Assert.Equal(new[] { 1.0, 2, 3, 4 }, ticks.Select(t => t.Bid));
		feed.Dispose();
	}

	[Fact]
	public async Task ReadTicks_WindowInsideHeldTicks_ReadsNoStoredTicks()
	{
		_history.Ticks.Add(TestTickHelper.AtMid(100, 3));
		var (feed, source) = await LoadedAsync();
		_stream.Raise(new MarketTick(1, "EURUSD", At(110), 4, 4));

		var ticks = source.ReadTicks(Symbol, null, At(105), At(200));

		Assert.Equal(new[] { 4.0 }, ticks.Select(t => t.Bid));
		feed.Dispose();
	}

	[Fact]
	public async Task ReadTicks_ReusesStoredBlockAsWindowStartAdvances()
	{
		_store.Seed(Table, [TestTickHelper.AtMid(10, 1), TestTickHelper.AtMid(20, 2)]);
		_history.Ticks.Add(TestTickHelper.AtMid(100, 3));
		var (feed, source) = await LoadedAsync();
		source.ReadTicks(Symbol, null, At(0), At(200));
		_store.InsertTicks(Table, [TestTickHelper.AtMid(30, 50)]);

		var ticks = source.ReadTicks(Symbol, null, At(15), At(200));

		Assert.Equal(new[] { 2.0, 3 }, ticks.Select(t => t.Bid));
		feed.Dispose();
	}
}
