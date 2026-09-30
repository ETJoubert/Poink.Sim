using System.Collections.Generic;
using Poink.Sim.Components.Charts;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// XGrid Signals feature values -- one entry per signal enabled in
	/// FeatureOptionsModel.XGridSignals.EnabledSignals, keyed by signal key so a display panel can
	/// pair each value with its label from XGridSignalCatalog.
	/// </summary>
	internal sealed record XGridSignalsFeature(IReadOnlyDictionary<string, double> SignalValues);

	/// <summary>
	/// TxFold feature values -- four values per timeframe selected in
	/// FeatureOptionsModel.TxFold.SelectedSeconds (six when IncludeHighLow is set), keyed by
	/// timeframe seconds, plus a shared VolumeProfile block (empty when IncludeVolumeProfile isn't
	/// set), CurrentPrice (null when IncludeCurrentPrice isn't set), and Levels (empty when
	/// IncludedLevels isn't set, keyed by kind so a display panel can pair each value with its label
	/// from TxFoldLevelOptions).
	/// </summary>
	internal sealed record TxFoldFeature(
		IReadOnlyDictionary<int, float[]> ValuesBySeconds,
		IReadOnlyList<float> VolumeProfile,
		float? CurrentPrice,
		IReadOnlyDictionary<TxFoldLevelKind, float> Levels
	);

	/// <summary>
	/// Trader State feature values -- mirrors TraderStateOptionsModel's fields 1:1, null where the
	/// matching option was not enabled, so reflection can pair each value with the option that
	/// produced it.
	/// </summary>
	internal sealed record TraderStateFeature(double? Unrealized, double? Realized);

	/// <summary>
	/// The Features tab's computed values for one step, structured (not flattened) so a display
	/// panel can walk it via reflection and show each feature's label and value -- FeatureVector is
	/// built from this for actual training.
	/// </summary>
	internal sealed record FeatureModel(XGridSignalsFeature XGridSignals, TxFoldFeature TxFold, TraderStateFeature TraderState);
}
