using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class TradeChartSettingsHelperTests
{
	private static TradePageSettings Legacy(bool profileLines, bool vwap) =>
		new(60, true, false, false, false, profileLines, vwap, 50, 0, 20, 20, null, null, false, false);

	[Fact]
	public void FromPersisted_LegacyBlob_SeedsIndicatorsFromOldToggles()
	{
		var chart = TradeChartSettingsHelper.FromPersisted(Legacy(profileLines: true, vwap: false));

		Assert.Equal((true, true, true, false), (chart.Indicators.ShowPoc, chart.Indicators.ShowVah, chart.Indicators.ShowVal, chart.Indicators.ShowVwap));
		Assert.Equal(ChartPanelSettings.Default.TxFoldSeconds, chart.TxFoldSeconds);
	}

	[Fact]
	public void RoundTrip_KeepsIndicatorsTxFoldAndCompare()
	{
		var chart = ChartPanelSettings.Default with
		{
			Indicators = new IndicatorSettings { ShowMedian = true, ShowDayLines = true },
			ShowTxFold = true,
			TxFoldSeconds = [60, 300],
			ShowCompare = true,
			Compare = new TradeCompareSettings(Mode: CompareSetMode.WindowAnchored, RatioTable: "live_ticks_gbpusd"),
		};

		var restored = TradeChartSettingsHelper.FromPersisted(TradeChartSettingsHelper.ToPersisted(chart, 60, 1, "EURUSD", false, false));

		Assert.True(restored.Indicators.ShowMedian && restored.Indicators.ShowDayLines);
		Assert.Equal(new[] { 60, 300 }, restored.TxFoldSeconds);
		Assert.True(restored.ShowTxFold && restored.ShowCompare);
		Assert.Equal(chart.Compare, restored.Compare);
	}

	[Fact]
	public void PopoutSettings_SurviveJsonRoundTrip()
	{
		var chart = ChartPanelSettings.Default with { ShowTxFold = true, Indicators = new IndicatorSettings { ShowRegression = true } };

		var url = ChartPopoutUrlHelper.BuildUrl(1, "EURUSD", 60, chart);
		var json = System.Uri.UnescapeDataString(url[(url.IndexOf("settings=") + "settings=".Length)..]);
		var parsed = ChartPopoutUrlHelper.ParseSettings(json);

		Assert.True(parsed.ShowTxFold && parsed.Indicators.ShowRegression);
		Assert.Equal(chart.TxFoldSeconds, parsed.TxFoldSeconds);
	}

	[Fact]
	public void PopoutSettings_Unreadable_FallBackToDefaults()
	{
		Assert.Same(ChartPanelSettings.Default, ChartPopoutUrlHelper.ParseSettings("{not json"));
	}
}
