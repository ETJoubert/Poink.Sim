namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// The network's input vector for one step -- FeatureModel's values flattened to a plain
	/// float[] for training.
	/// </summary>
	internal sealed record FeatureVector(float[] Values);
}
