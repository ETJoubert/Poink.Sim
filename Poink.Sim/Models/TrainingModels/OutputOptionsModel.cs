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
	/// Rules that restrict which actions are legal to choose. Every rule defaults on, as in
	/// BrainBuilder's equivalent card: the network has no position input, so the masks are what stop
	/// Trade Outcome learning from do-nothing picks. The XGrid-dependent rules (Long/Short
	/// require a directional signal, Close requires a key level) are left out for now.
	/// </summary>
	internal sealed record ActionMaskingOptionsModel(
		bool PositionRequiredForClose = true,
		bool FlatRequiredForEntry = true
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
