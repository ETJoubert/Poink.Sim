using System.Collections.Generic;
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
	/// (see TxFoldStats.BuildBar).
	/// </summary>
	internal sealed record TxFoldOptionsModel(IReadOnlyList<int>? SelectedSeconds = null, BarAggregationKind BarKind = BarAggregationKind.Ohlc);

	/// <summary>
	/// Trader State encoder settings -- which account/position fields are included, plus the
	/// trailing list lengths for Outcomes/PositionLevel/Direction history.
	/// </summary>
	internal sealed record TraderStateOptionsModel(
		bool IncludePosition = false,
		bool IncludePositionPrice = false,
		bool IncludePositionTicks = false,
		bool IncludePositionDistance = false,
		bool IncludeCurrentPrice = false,
		bool IncludeCash = false,
		bool IncludeRealized = false,
		bool IncludeUnrealized = false,
		bool IncludeEquity = false,
		bool IncludeMargin = false,
		bool IncludeMarginLevel = false,
		bool IncludePositionCount = false,
		bool IncludeOutcomes = false,
		bool IncludePositionLevel = false,
		bool IncludeDirection = false,
		int OutcomeListLength = 5,
		int PositionLevelListLength = 5,
		int DirectionListLength = 5
	);

	/// <summary>
	/// The Features tab's encoder selections -- TrainPageV2's counterpart to EncoderSizeOptions,
	/// scoped to the sources it still exposes (TxFold replaces BarWindow/VolumeProfile/TimeProfile/
	/// SessionClock/SpreadVolatility; XGrid Signals and Trader State carry over unchanged).
	/// </summary>
	internal sealed record FeatureOptionsModel(XGridSignalsOptionsModel XGridSignals, TxFoldOptionsModel TxFold, TraderStateOptionsModel TraderState);
}
