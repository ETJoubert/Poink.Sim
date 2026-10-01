using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>Which trade actions the network can output.</summary>
	internal sealed record ActionsOptionsModel(
		bool IncludeHold = true,
		bool IncludeEnterLong = true,
		bool IncludeEnterShort = true,
		bool IncludeClose = true
	);

	/// <summary>
	/// Rules that restrict which actions are legal to choose. Every rule defaults off here, unlike
	/// BrainBuilder's equivalent card where they default on. The XGrid-dependent rules (Long/Short
	/// require a directional signal, Close requires a key level) are left out for now.
	/// </summary>
	internal sealed record ActionMaskingOptionsModel(
		bool PositionRequiredForClose = false,
		bool FlatRequiredForEntry = false
	);

	/// <summary>How the network's raw outputs are turned into a chosen action.</summary>
	internal sealed record ActionDecodingOptionsModel(
		DecisionDecodingKind DecisionDecoding = DecisionDecodingKind.Softmax,
		double DecisionTemperature = 1.0
	);

	/// <summary>The Output tab's action set, masking rules, and decoding settings.</summary>
	internal sealed record OutputOptionsModel(
		ActionsOptionsModel Actions,
		ActionMaskingOptionsModel ActionMasking,
		ActionDecodingOptionsModel ActionDecoding
	);
}
