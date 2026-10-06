using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.MarketData.History;

public class TickHistoryServiceTests
{
	private const string Symbol = "EURUSD";
	private const string LiveTable = "live_ticks_eurusd";
	private const string DemoTable = "demo_ticks_eurusd";

	private static readonly DateTime Monday = TestTickHelper.Origin;
	private static readonly DateTime Tuesday = Monday.AddDays(1);
	private static readonly DateTime Wednesday = Monday.AddDays(2);
	private static readonly DateTime Now = Wednesday.AddHours(10);

	private readonly InMemoryTickRepository _store = new();
	private readonly FakeCTraderHistory _api = new();

	private TickHistoryService CreateService() => new(_store, _store, _api, NullLogger<TickHistoryService>.Instance);

	private static Tick At(DateTime utc) => new(utc, 1.1, 1.1002);

	private static TickHistoryRequest Request(DateTime from) => new(1, Symbol, from, Now, BackfillTrigger.User);

	[Fact]
	public async Task LoadAsync_ReadsPastDaysFromDatabase_FetchesOnlyTodayFromApi()
	{
		_store.Seed(LiveTable, [At(Monday.AddHours(9)), At(Tuesday.AddHours(9))]);
		_api.Available.AddRange([At(Tuesday.AddHours(10)), At(Wednesday.AddHours(1)), At(Wednesday.AddHours(2))]);

		var result = await CreateService().LoadAsync(Request(Monday));

		Assert.Equal(new[] { (Wednesday, Now) }, _api.Requests);
		Assert.Equal(
			new[] { Monday.AddHours(9), Tuesday.AddHours(9), Wednesday.AddHours(1), Wednesday.AddHours(2) },
			result.Ticks.Select(t => t.Timestamp)
		);
		Assert.Equal((2, 2), (result.DatabaseTickCount, result.ApiTickCount));
		Assert.Empty(result.MissingDays);
	}

	[Fact]
	public async Task LoadAsync_CommitsApiTicksAndRefreshesTheirDay()
	{
		_api.Available.Add(At(Wednesday.AddHours(1)));

		await CreateService().LoadAsync(Request(Wednesday));

		Assert.Equal(new[] { Wednesday.AddHours(1) }, _store.All(LiveTable).Select(t => t.Timestamp));
		Assert.Contains((LiveTable, DateOnly.FromDateTime(Wednesday)), _store.RefreshedDays);
		Assert.Equal(BackfillTrigger.User, Assert.Single(_store.BackfillLog).Trigger);
	}

	[Fact]
	public async Task LoadAsync_TodayAlreadyStored_FetchesOnlyAfterLatestStoredTick()
	{
		var lastStored = Wednesday.AddHours(8);
		_store.Seed(LiveTable, [At(Wednesday.AddHours(1)), At(lastStored)]);

		var result = await CreateService().LoadAsync(Request(Wednesday));

		Assert.Equal(lastStored.AddTicks(1), Assert.Single(_api.Requests).From);
		Assert.Equal(2, result.DatabaseTickCount);
	}

	[Fact]
	public async Task LoadAsync_DemoAccount_ReadsLiveHistoryAndStoresTodayInDemoTable()
	{
		_api.UseLiveEnvironment = false;
		_store.Seed(LiveTable, [At(Tuesday.AddHours(9))]);
		_api.Available.Add(At(Wednesday.AddHours(1)));

		var result = await CreateService().LoadAsync(Request(Tuesday));

		Assert.Equal((LiveTable, DemoTable), (result.HistoryTable, result.TodayTable));
		Assert.Equal(2, result.Ticks.Count);
		Assert.Single(_store.All(DemoTable));
		Assert.Single(_store.All(LiveTable));
	}

	[Fact]
	public async Task LoadAsync_ApiUnavailable_ReturnsDatabaseTicksAndFlagsIt()
	{
		_api.CanFetchHistory = false;
		_store.Seed(LiveTable, [At(Tuesday.AddHours(9))]);

		var result = await CreateService().LoadAsync(Request(Tuesday));

		Assert.True(result.ApiUnavailable);
		Assert.Empty(_api.Requests);
		Assert.Single(result.Ticks);
	}

	[Fact]
	public async Task LoadAsync_DayMissingFromDatabase_IsReportedNotFetched()
	{
		_store.Seed(LiveTable, [At(Tuesday.AddHours(9))]);

		var result = await CreateService().LoadAsync(Request(Monday));

		Assert.Equal(new[] { DateOnly.FromDateTime(Monday) }, result.MissingDays);
		Assert.DoesNotContain(_api.Requests, r => r.From < Wednesday);
	}

	[Fact]
	public async Task LoadAsync_AccountSwitchDuringFetch_DiscardsAndDoesNotStoreApiTicks()
	{
		_api.Available.Add(At(Wednesday.AddHours(1)));
		_api.DuringFetch = () => _api.SelectedAccountId = 2;

		var result = await CreateService().LoadAsync(Request(Wednesday));

		Assert.Equal(0, result.ApiTickCount);
		Assert.Empty(_store.All(LiveTable));
	}

	[Fact]
	public async Task CatchUpTodayAsync_FetchesFromLatestStoredTickUpToRequestedTime()
	{
		var lastStored = Wednesday.AddHours(3);
		var upTo = Wednesday.AddHours(9);
		_store.Seed(LiveTable, [At(lastStored)]);
		_api.Available.AddRange([At(Wednesday.AddHours(5)), At(upTo)]);

		var fetched = await CreateService().CatchUpTodayAsync(1, Symbol, upTo, BackfillTrigger.Worker);

		Assert.Equal(1, fetched);
		Assert.Equal((lastStored.AddTicks(1), upTo), Assert.Single(_api.Requests));
		Assert.Equal(BackfillTrigger.Worker, Assert.Single(_store.BackfillLog).Trigger);
	}
}
