namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// TrainPageV2's full configuration -- one options model per tab, composed together the way
	/// BrainRecipe composes EncoderSizeOptions for the older BrainBuilder-based system.
	/// BarWidthSeconds sits here rather than under Features or Training because both read it.
	/// </summary>
	internal sealed record TrainingOptionsModel(
		DataOptionsModel Data,
		FeatureOptionsModel Features,
		NetworkOptionsModel Network,
		OutputOptionsModel Output,
		TrainingTabOptionsModel Training,
		PlaybackOptionsModel Playback,
		int BarWidthSeconds = 60
	);
}
