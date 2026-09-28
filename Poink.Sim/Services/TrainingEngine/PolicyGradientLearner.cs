using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Turns one episode's recorded decisions into a network weight update. REINFORCE: discount
/// rewards into returns, normalize by the episode's own mean/std as a variance-reducing baseline,
/// backprop each step's advantage-weighted softmax gradient, apply one averaged SGD update. Only
/// "Policy Gradient (REINFORCE)" is implemented; other strategy names throw. No entropy-bonus
/// regularization -- a known simplification, left out rather than guessed at.
/// </summary>
internal sealed class PolicyGradientLearner
{
	public const string ReinforceStrategyName = "Policy Gradient (REINFORCE)";

	private const double DiscountFactor = 0.99;

	public static void EnsureSupportedStrategy(string strategyName)
	{
		if (strategyName != ReinforceStrategyName)
			throw new NotSupportedException($"{strategyName} isn't implemented yet -- only {ReinforceStrategyName} is built so far.");
	}

	public void Learn(MlpNetwork network, ActionSpace actionSpace, IReadOnlyList<DecisionTrace> traces, LearningOptionsModel learning)
	{
		EnsureSupportedStrategy(learning.TrainingStrategyName);
		if (traces.Count == 0)
			return;

		var returns = DiscountedReturns(traces);
		var mean = returns.Average();
		var std = Math.Sqrt(returns.Select(r => (r - mean) * (r - mean)).Average());

		var steps = new List<(List<float[]> Activations, float[] OutputGradient)>(traces.Count);
		for (var i = 0; i < traces.Count; i++)
		{
			var advantage = (returns[i] - mean) / (std + 1e-8);
			steps.Add((traces[i].Activations, PolicyGradient(traces[i].RawOutputs, actionSpace, traces[i].ActionIndex, advantage)));
		}

		ApplyGradient(network, steps, learning.LearningRate);
	}

	private static double[] DiscountedReturns(IReadOnlyList<DecisionTrace> traces)
	{
		var returns = new double[traces.Count];
		var runningReturn = 0.0;
		for (var i = traces.Count - 1; i >= 0; i--)
		{
			runningReturn = traces[i].Reward + DiscountFactor * runningReturn;
			returns[i] = runningReturn;
		}
		return returns;
	}

	// Standard softmax-cross-entropy gradient (probability - one-hot), scaled by advantage and by
	// 1/T for the temperature division's chain rule. Continuous output slots stay zero -- untrained.
	private static float[] PolicyGradient(float[] rawOutputs, ActionSpace actionSpace, int actionIndex, double advantage)
	{
		var directionWidth = actionSpace.DirectionOutputWidth;
		var probabilities = actionSpace.Probabilities(rawOutputs[..directionWidth]);
		var temperatureScale = Math.Max(actionSpace.DecisionTemperature, 1e-8);

		var gradient = new float[rawOutputs.Length];
		for (var i = 0; i < directionWidth; i++)
		{
			var indicator = i == actionIndex ? 1.0 : 0.0;
			gradient[i] = (float)(advantage * (probabilities[i] - indicator) / temperatureScale);
		}
		return gradient;
	}

	// Backpropagates and averages one episode's worth of steps into a single update, matching
	// REINFORCE's one gradient over the whole episode's mean loss, not a step per decision.
	private static void ApplyGradient(MlpNetwork network, List<(List<float[]> Activations, float[] OutputGradient)> steps, double learningRate)
	{
		var weightGradients = network.Weights.Select(w => new float[w.GetLength(0), w.GetLength(1)]).ToList();
		var biasGradients = network.Biases.Select(b => new float[b.Length]).ToList();

		foreach (var (activations, outputGradient) in steps)
		{
			var (stepWeightGradients, stepBiasGradients) = Backward(network, activations, outputGradient);
			Accumulate(weightGradients, stepWeightGradients);
			Accumulate(biasGradients, stepBiasGradients);
		}

		Scale(weightGradients, 1.0 / steps.Count);
		Scale(biasGradients, 1.0 / steps.Count);
		ApplyGradients(network, weightGradients, biasGradients, learningRate);
	}

	// Standard backprop: dz is the pre-activation gradient (ReLU' masks it for hidden layers),
	// dW = dz outer prev-activation, db = dz, dPrev propagates dz through this layer's weights.
	private static (List<float[,]> WeightGradients, List<float[]> BiasGradients) Backward(
		MlpNetwork network,
		List<float[]> activations,
		float[] outputGradient
	)
	{
		var layerCount = network.Weights.Count;
		var weightGradients = new List<float[,]>(new float[layerCount][,]);
		var biasGradients = new List<float[]>(new float[layerCount][]);

		var dCurrent = outputGradient;
		for (var layer = layerCount - 1; layer >= 0; layer--)
		{
			var isOutputLayer = layer == layerCount - 1;
			var layerInput = activations[layer];
			var layerOutput = activations[layer + 1];
			var outputSize = network.Biases[layer].Length;
			var inputSize = layerInput.Length;

			var dz = new float[outputSize];
			for (var o = 0; o < outputSize; o++)
				dz[o] = isOutputLayer ? dCurrent[o] : dCurrent[o] * (layerOutput[o] > 0f ? 1f : 0f);

			var weightGradient = new float[outputSize, inputSize];
			for (var o = 0; o < outputSize; o++)
				for (var i = 0; i < inputSize; i++)
					weightGradient[o, i] = dz[o] * layerInput[i];
			weightGradients[layer] = weightGradient;
			biasGradients[layer] = dz;

			if (layer == 0)
				continue;

			var dPrev = new float[inputSize];
			for (var i = 0; i < inputSize; i++)
			{
				float sum = 0;
				for (var o = 0; o < outputSize; o++)
					sum += network.Weights[layer][o, i] * dz[o];
				dPrev[i] = sum;
			}
			dCurrent = dPrev;
		}

		return (weightGradients, biasGradients);
	}

	private static void ApplyGradients(MlpNetwork network, List<float[,]> weightGradients, List<float[]> biasGradients, double learningRate)
	{
		for (var layer = 0; layer < network.Weights.Count; layer++)
		{
			var weights = network.Weights[layer];
			var weightGradient = weightGradients[layer];
			for (var o = 0; o < weights.GetLength(0); o++)
				for (var i = 0; i < weights.GetLength(1); i++)
					weights[o, i] -= (float)(learningRate * weightGradient[o, i]);

			var biases = network.Biases[layer];
			var biasGradient = biasGradients[layer];
			for (var o = 0; o < biases.Length; o++)
				biases[o] -= (float)(learningRate * biasGradient[o]);
		}
	}

	private static void Accumulate(List<float[,]> totals, List<float[,]> increments)
	{
		for (var layer = 0; layer < totals.Count; layer++)
		{
			var total = totals[layer];
			var increment = increments[layer];
			for (var o = 0; o < total.GetLength(0); o++)
				for (var i = 0; i < total.GetLength(1); i++)
					total[o, i] += increment[o, i];
		}
	}

	private static void Accumulate(List<float[]> totals, List<float[]> increments)
	{
		for (var layer = 0; layer < totals.Count; layer++)
			for (var i = 0; i < totals[layer].Length; i++)
				totals[layer][i] += increments[layer][i];
	}

	private static void Scale(List<float[,]> matrices, double factor)
	{
		foreach (var matrix in matrices)
			for (var o = 0; o < matrix.GetLength(0); o++)
				for (var i = 0; i < matrix.GetLength(1); i++)
					matrix[o, i] *= (float)factor;
	}

	private static void Scale(List<float[]> vectors, double factor)
	{
		foreach (var vector in vectors)
			for (var i = 0; i < vector.Length; i++)
				vector[i] *= (float)factor;
	}
}
