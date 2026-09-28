using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Everything PolicyGradientLearner needs from one decision step -- Activations comes straight
/// from ForwardWithActivations so it never has to recompute a forward pass.
/// </summary>
internal sealed record DecisionTrace(List<float[]> Activations, float[] RawOutputs, int ActionIndex, double Reward);
