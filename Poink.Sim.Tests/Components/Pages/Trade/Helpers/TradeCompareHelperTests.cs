using System;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class TradeCompareHelperTests
{
	private static HlocAvdtBar BarAt(int minute, double close) =>
		TestBarHelper.Closing(close) with
		{
			OpenTime = TestTickHelper.Origin.AddMinutes(minute),
			CloseTime = TestTickHelper.Origin.AddMinutes(minute).AddSeconds(59),
		};

	private static readonly HlocAvdtBar[] Bars = [BarAt(0, 1.0), BarAt(1, 1.1), BarAt(2, 1.2)];

	[Fact]
	public void TxFoldAnchorBar_AtOffsetZero_SkipsTheFormingBar()
	{
		Assert.Equal(1.1, TradeCompareHelper.TxFoldAnchorBar(Bars, 0)!.Value.Close);
	}

	[Fact]
	public void TxFoldAnchorBar_WithOffset_UsesWindowsLastBar()
	{
		Assert.Equal(1.2, TradeCompareHelper.TxFoldAnchorBar(Bars, 3)!.Value.Close);
	}

	[Fact]
	public void TxFoldAnchorBar_SingleOrNoBar()
	{
		Assert.Equal(1.0, TradeCompareHelper.TxFoldAnchorBar([Bars[0]], 0)!.Value.Close);
		Assert.Null(TradeCompareHelper.TxFoldAnchorBar(Array.Empty<HlocAvdtBar>(), 0));
	}

	[Fact]
	public void ReferenceClose_UsesBarAtOrBeforeAnchor()
	{
		Assert.Equal(1.1, TradeCompareHelper.ReferenceClose(Bars, TestTickHelper.Origin.AddMinutes(1).AddSeconds(30), TestTickHelper.Origin));
	}

	[Fact]
	public void ReferenceClose_DataStartingAfterAnchor_FallsBackToFirstBarInWindow()
	{
		HlocAvdtBar[] late = [BarAt(5, 2.0), BarAt(6, 2.1)];

		Assert.Equal(2.0, TradeCompareHelper.ReferenceClose(late, TestTickHelper.Origin, TestTickHelper.Origin));
	}

	[Fact]
	public void ReferenceClose_NoBars_IsNull()
	{
		Assert.Null(TradeCompareHelper.ReferenceClose(Array.Empty<HlocAvdtBar>(), TestTickHelper.Origin, TestTickHelper.Origin));
	}
}
