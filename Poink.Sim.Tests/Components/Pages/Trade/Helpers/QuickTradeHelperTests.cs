using System;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Services.CTrader;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class QuickTradeHelperTests
{
	private const long Nasdaq = 1107;
	private const long Dow = 1287;

	private static CTraderPositionInfo Position(long id, long symbolId, bool isLong, double volume = 1) =>
		new(id, symbolId, "X", isLong, volume, 100, DateTime.UtcNow, null, null, 0);

	[Fact]
	public void SidePositions_KeepsOnlyTheSymbolsPositionsOnThatSide()
	{
		var positions = new[]
		{
			Position(1, Nasdaq, isLong: true),
			Position(2, Nasdaq, isLong: false),
			Position(3, Dow, isLong: true),
			Position(4, Nasdaq, isLong: true),
		};

		var longs = QuickTradeHelper.SidePositions(positions, Nasdaq, isLong: true);

		Assert.Equal([1L, 4L], longs.Select(p => p.PositionId));
	}

	[Fact]
	public void OrderMargin_PricesAtTheMidQuoteOverLeverage()
	{
		Assert.Equal(12.5, QuickTradeHelper.OrderMargin(0.01, 24999, 25001, 20));
	}

	[Theory]
	[InlineData(null, 25001d, 20d)]
	[InlineData(24999d, null, 20d)]
	[InlineData(24999d, 25001d, null)]
	[InlineData(24999d, 25001d, 0d)]
	public void OrderMargin_MissingQuoteOrLeverage_IsUnknown(double? bid, double? ask, double? leverage)
	{
		Assert.Null(QuickTradeHelper.OrderMargin(0.01, bid, ask, leverage));
	}

	[Theory]
	[InlineData(500d, 12.5d, 487.5d)]
	[InlineData(10d, 12.5d, -2.5d)]
	[InlineData(null, 12.5d, null)]
	[InlineData(500d, null, null)]
	public void MarginFreeAfter_SubtractsTheOrdersMargin(double? available, double? margin, double? expected)
	{
		Assert.Equal(expected, QuickTradeHelper.MarginFreeAfter(available, margin));
	}

	[Fact]
	public void SummarizeClose_AllClosed_Succeeds()
	{
		var result = QuickTradeHelper.SummarizeClose(
			isLong: true,
			[new CTraderOrderResult(true, "Position 1 closed."), new CTraderOrderResult(true, "Position 2 closed.")]
		);

		Assert.True(result.Success);
		Assert.Equal("Closed 2 long position(s).", result.Message);
	}

	[Fact]
	public void SummarizeClose_SomeFailed_FailsAndNamesTheErrors()
	{
		var result = QuickTradeHelper.SummarizeClose(
			isLong: false,
			[
				new CTraderOrderResult(true, "Position 1 closed."),
				new CTraderOrderResult(false, "Close rejected: MarketClosed"),
			]
		);

		Assert.False(result.Success);
		Assert.Equal("Closed 1 of 2 short position(s): Close rejected: MarketClosed", result.Message);
	}

	[Theory]
	[InlineData(0.1, null, 0.1)]
	[InlineData(null, null, 0.01)]
	[InlineData(0.5, null, 0.01)]
	[InlineData(0.01, 0.1, 0.1)]
	[InlineData(0.1, 0.1, 0.1)]
	[InlineData(1d, 0.1, 1d)]
	public void ChooseVolume_KeepsAnAllowedStoredPresetElseTheSmallestAllowed(
		double? stored,
		double? minVolume,
		double expected
	)
	{
		Assert.Equal(expected, QuickTradeHelper.ChooseVolume(stored, QuickTradeHelper.VolumePresets, minVolume));
	}

	[Fact]
	public void ChooseVolume_SymbolMinimumAboveEveryPreset_IsNull()
	{
		Assert.Null(QuickTradeHelper.ChooseVolume(1, QuickTradeHelper.VolumePresets, minVolume: 5));
	}

	[Theory]
	[InlineData(0.37, 0.1, 0.01, 0.4)]
	[InlineData(0.05, 0.01, 0.1, 0.1)]
	[InlineData(0.25, 0.1, null, 0.3)]
	[InlineData(1.234, null, null, 1.234)]
	[InlineData(0.30000000000000004, null, 0.01, 0.3)]
	public void SnapVolume_RoundsToTheStepAndLiftsToTheMinimum(
		double volume,
		double? volumeStep,
		double? minVolume,
		double expected
	)
	{
		Assert.Equal(expected, QuickTradeHelper.SnapVolume(volume, minVolume, volumeStep), 10);
	}
}
