using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Services;

public class BreakevenCloseServiceTests
{
	private const long Nasdaq = 1107;

	private static readonly BreakevenSide Longs = new(Nasdaq, IsLong: true);
	private static readonly BreakevenSide Shorts = new(Nasdaq, IsLong: false);

	private readonly FakeTickStream _stream = new();
	private readonly FakeCloseExecution _execution = new();
	private readonly FakeAccountAccess _account = new();
	private readonly BreakevenCloseService _service;

	public BreakevenCloseServiceTests()
	{
		_service = new BreakevenCloseService(_stream, _execution, _account, NullLogger<BreakevenCloseService>.Instance);
	}

	private static CTraderPositionInfo Position(long id, bool isLong, double entry) =>
		new(id, Nasdaq, "NAS100", isLong, 1, entry, DateTime.UtcNow, null, null, 0);

	private void Tick(double bid, double ask) =>
		_stream.Raise(new MarketTick(Nasdaq, "NAS100", DateTime.UtcNow, bid, ask));

	[Fact]
	public void ArmedLong_WaitsUntilTheBidIsBackToEntryThenClosesOnce()
	{
		_stream.Positions = [Position(1, isLong: true, entry: 100)];
		_service.Arm(Longs);

		Tick(99.5, 99.7);
		Assert.Empty(_execution.Closed);

		Tick(100, 100.2);
		Tick(100.3, 100.5);
		Assert.Equal([1L], _execution.Closed);
	}

	[Fact]
	public void ArmedSide_ClosesEachPositionOnItsOwn()
	{
		_stream.Positions = [Position(1, isLong: true, entry: 100), Position(2, isLong: true, entry: 101)];
		_service.Arm(Longs);

		Tick(100.5, 100.7);
		Assert.Equal([1L], _execution.Closed);

		_stream.Positions = [Position(2, isLong: true, entry: 101)];
		Tick(101, 101.2);
		Assert.Equal([1L, 2L], _execution.Closed);
	}

	[Fact]
	public void ArmedShort_ClosesOnceTheAskIsBackToEntry()
	{
		_stream.Positions = [Position(1, isLong: false, entry: 100), Position(2, isLong: true, entry: 50)];
		_service.Arm(Shorts);

		Tick(99.6, 100.1);
		Assert.Empty(_execution.Closed);

		Tick(99.8, 100);
		Assert.Equal([1L], _execution.Closed);
	}

	[Fact]
	public void Arm_ClosesAtOnceWhenTheQuoteIsAlreadyAtBreakeven()
	{
		_stream.Positions = [Position(1, isLong: true, entry: 100)];
		_stream.Quotes = [new CTraderSymbolQuote(Nasdaq, "NAS100", 100.4, 100.6, DateTime.UtcNow)];

		_service.Arm(Longs);

		Assert.Equal([1L], _execution.Closed);
	}

	[Fact]
	public void ArmedSide_DisarmsOnceNoPositionsAreLeft()
	{
		var changes = 0;
		_service.Changed += () => changes++;
		_stream.Positions = [Position(1, isLong: true, entry: 100)];
		_service.Arm(Longs);

		_stream.Positions = [];
		Tick(99, 99.2);

		Assert.False(_service.IsArmed(Longs));
		Assert.Equal(2, changes);
	}

	[Fact]
	public void FailedClose_DisarmsTheSideAndReportsTheFailure()
	{
		var reports = new List<CTraderOrderResult>();
		_service.CloseReported += reports.Add;
		_execution.Result = new CTraderOrderResult(false, "market closed");
		_stream.Positions = [Position(1, isLong: true, entry: 100)];
		_service.Arm(Longs);

		Tick(100, 100.2);

		Assert.False(_service.IsArmed(Longs));
		var report = Assert.Single(reports);
		Assert.False(report.Success);
		Assert.Contains("market closed", report.Message);
	}

	[Fact]
	public void AccountSwitch_DisarmsEverySide()
	{
		_stream.Positions = [Position(1, isLong: true, entry: 100), Position(2, isLong: false, entry: 100)];
		_service.Arm(Longs);
		_service.Arm(Shorts);

		_account.Select(2);
		Tick(100.5, 100.7);

		Assert.Empty(_service.ArmedSides);
		Assert.Empty(_execution.Closed);
	}

	[Fact]
	public void ArmedSides_ListsWhatIsArmed()
	{
		_service.Arm(Longs);

		Assert.Equal([Longs], _service.ArmedSides);
	}

	[Fact]
	public void Disarm_StopsWatchingTheSide()
	{
		_stream.Positions = [Position(1, isLong: true, entry: 100)];
		_service.Arm(Longs);
		_service.Disarm(Longs);

		Tick(100.5, 100.7);

		Assert.Empty(_execution.Closed);
	}

	private sealed class FakeCloseExecution : ITradeExecutionAccess
	{
		public List<long> Closed { get; } = [];

		public CTraderOrderResult Result { get; set; } = new(true, "closed");

		public bool UseLiveEnvironment => false;

		public Task<CTraderOrderResult> PlaceOrderAsync(
			long symbolId,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();

		public Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<CTraderOrderResult> ClosePositionAsync(long positionId, CancellationToken cancellationToken = default)
		{
			Closed.Add(positionId);
			return Task.FromResult(Result);
		}
	}
}
