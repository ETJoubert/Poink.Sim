using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Services.MarketData.Recording;
using Xunit;

namespace Poink.Sim.Tests.Services.MarketData.History;

public class TodayTickReloadServiceTests
{
	private const string LiveTable = "live_ticks_eurusd";
	private const string DemoTable = "demo_ticks_eurusd";

	private static readonly DateTime Today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

	private readonly TestSupport.InMemoryTickRepository _store = new();
	private readonly TestSupport.FakeCTraderHistory _api = new();
	private readonly FakeRecorder _recorder = new();

	private TodayTickReloadService CreateService() => new(_api, _store, _recorder, NullLogger<TodayTickReloadService>.Instance);

	private static Tick At(DateTime utc, double bid = 1.1) => new(utc, bid, bid);

	[Fact]
	public async Task ReloadAsync_ReplacesTodaysTicksAndKeepsEarlierDays()
	{
		_store.Seed(LiveTable, [At(Today.AddDays(-1).AddHours(5)), At(Today.AddMilliseconds(1), 9), At(Today.AddMilliseconds(2), 9)]);
		_api.Available.Add(At(Today.AddMilliseconds(3), 1.2));

		var result = await CreateService().ReloadAsync(1, "EURUSD");

		Assert.True(result.Succeeded);
		Assert.Equal((2L, 1L), (result.Deleted, result.Inserted));
		Assert.Equal(new[] { 1.1, 1.2 }, _store.All(LiveTable).Select(t => t.Bid));
		Assert.Contains((LiveTable, DateOnly.FromDateTime(Today)), _store.RefreshedDays);
		Assert.Equal(BackfillTrigger.User, Assert.Single(_store.BackfillLog).Trigger);
	}

	[Fact]
	public async Task ReloadAsync_DropsBufferedLiveTicksBeforeFetchEnd()
	{
		await CreateService().ReloadAsync(1, "EURUSD");

		var (table, beforeUtc) = Assert.Single(_recorder.Discards);
		Assert.Equal(LiveTable, table);
		Assert.True(beforeUtc >= Today);
	}

	[Fact]
	public async Task ReloadAsync_FetchFails_LeavesStoredTicksUntouched()
	{
		_store.Seed(LiveTable, [At(Today.AddMilliseconds(1), 9)]);
		_api.DuringFetch = () => throw new InvalidOperationException("cTrader down");

		var result = await CreateService().ReloadAsync(1, "EURUSD");

		Assert.False(result.Succeeded);
		Assert.Single(_store.All(LiveTable));
	}

	[Fact]
	public async Task ReloadAsync_AccountSwitchDuringFetch_LeavesStoredTicksUntouched()
	{
		_store.Seed(LiveTable, [At(Today.AddMilliseconds(1), 9)]);
		_api.Available.Add(At(Today.AddMilliseconds(3), 1.2));
		_api.DuringFetch = () => _api.SelectedAccountId = 2;

		var result = await CreateService().ReloadAsync(1, "EURUSD");

		Assert.False(result.Succeeded);
		Assert.Equal(9, Assert.Single(_store.All(LiveTable)).Bid);
	}

	[Fact]
	public async Task ReloadAsync_DemoAccount_WritesDemoTable()
	{
		_api.UseLiveEnvironment = false;
		_api.Available.Add(At(Today.AddMilliseconds(3)));

		var result = await CreateService().ReloadAsync(1, "EURUSD");

		Assert.Equal(DemoTable, result.Table);
		Assert.Single(_store.All(DemoTable));
		Assert.Empty(_store.All(LiveTable));
	}

	[Fact]
	public async Task ReloadAsync_ApiUnavailable_ReportsWithoutWriting()
	{
		_api.CanFetchHistory = false;

		var result = await CreateService().ReloadAsync(1, "EURUSD");

		Assert.False(result.Succeeded);
		Assert.Empty(_api.Requests);
	}

	private sealed class FakeRecorder : ILiveTickRecorder
	{
		public List<(string Table, DateTime BeforeUtc)> Discards { get; } = new();

		public bool IsRecording => true;

		public IAsyncDisposable Acquire() => throw new NotSupportedException();

		public int DiscardBuffered(string table, DateTime beforeUtc)
		{
			Discards.Add((table, beforeUtc));
			return 0;
		}
	}
}
