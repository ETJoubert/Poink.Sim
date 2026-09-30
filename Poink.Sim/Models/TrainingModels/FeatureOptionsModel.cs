using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// XGrid Signals encoder settings. Pip size and window are derived elsewhere (selected symbol,
	/// column count) rather than stored here.
	/// </summary>
	internal sealed record XGridSignalsOptionsModel(
		IReadOnlyList<string> EnabledSignals,
		double TopPercent = 10,
		int ColCount = 20,
		int RowCount = 20
	);

	/// <summary>
	/// TxFold encoder settings -- one selected timeframe per element, four values per timeframe
	/// (see TxFoldStats.BuildBar), plus two more per timeframe when IncludeHighLow is set, a shared
	/// VolumeProfileBucketCount-length block when IncludeVolumeProfile is set, one shared scalar
	/// when IncludeCurrentPrice is set, and one shared scalar per level in IncludedLevels (POC/VAH/
	/// VAL/VWAP/AVE/MED/REG, computed once over the ticks behind the displayed bars, same as the
	/// TxFold chart's own overlay toggles). Normalize rescales every one of those values (including
	/// CurrentPrice and the levels) relative to the largest selected timeframe's true High/Low, same
	/// math the TxFold chart's own "%" toggle uses for display.
	/// </summary>
	internal sealed record TxFoldOptionsModel(
		IReadOnlyList<int>? SelectedSeconds = null,
		BarAggregationKind BarKind = BarAggregationKind.Ohlc,
		bool IncludeHighLow = false,
		bool IncludeVolumeProfile = false,
		int VolumeProfileBucketCount = 20,
		bool IncludeCurrentPrice = false,
		IReadOnlyList<TxFoldLevelKind>? IncludedLevels = null,
		bool Normalize = false
	);

	/// <summary>
	/// Trader State encoder settings -- unrealized/realized profit/loss as a percent of cash balance.
	/// </summary>
	internal sealed record TraderStateOptionsModel(bool IncludeUnrealized = false, bool IncludeRealized = false);

	/// <summary>
	/// The Features tab's encoder selections -- TrainPageV2's counterpart to EncoderSizeOptions,
	/// scoped to the sources it still exposes (TxFold replaces BarWindow/VolumeProfile/TimeProfile/
	/// SessionClock/SpreadVolatility; XGrid Signals and Trader State carry over unchanged).
	/// </summary>
	internal sealed record FeatureOptionsModel(XGridSignalsOptionsModel XGridSignals, TxFoldOptionsModel TxFold, TraderStateOptionsModel TraderState);
}
