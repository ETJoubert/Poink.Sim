using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// A fully-connected feed-forward network: Dense(input -> hidden[0]) -ReLU-> ... -> Dense(hidden[^1]
/// -> output), output layer linear (ActionSpace decodes it downstream). Weights/biases are public
/// and mutable so PolicyGradientLearner can update them in place -- this class only knows how to
/// run itself forward, not how to learn.
/// </summary>
internal sealed class MlpNetwork
{
	public int InputSize { get; }

	public IReadOnlyList<int> HiddenSizes { get; }

	public int OutputSize { get; }

	// Layer i: Weights[i] is [outputs x inputs] (row-major), Biases[i] is [outputs].
	public IReadOnlyList<float[,]> Weights { get; }

	public IReadOnlyList<float[]> Biases { get; }

	public MlpNetwork(
		int inputSize,
		IReadOnlyList<int> hiddenSizes,
		int outputSize,
		IReadOnlyList<float[,]> weights,
		IReadOnlyList<float[]> biases
	)
	{
		InputSize = inputSize;
		HiddenSizes = hiddenSizes;
		OutputSize = outputSize;
		Weights = weights;
		Biases = biases;
	}

	public float[] Forward(IReadOnlyList<float> input) => ForwardWithActivations(input).Output;

	// Activations[0] is the input, Activations[i+1] is layer i's output -- PolicyGradientLearner
	// needs the whole chain to backprop, not just the final output.
	public (float[] Output, List<float[]> Activations) ForwardWithActivations(IReadOnlyList<float> input)
	{
		if (input.Count != InputSize)
			throw new ArgumentException($"Expected {InputSize} inputs, got {input.Count}.", nameof(input));

		var activations = new List<float[]>(Weights.Count + 1) { input.ToArray() };
		var current = activations[0];
		for (var layer = 0; layer < Weights.Count; layer++)
		{
			var isOutputLayer = layer == Weights.Count - 1;
			current = DenseLayer(current, Weights[layer], Biases[layer], applyRelu: !isOutputLayer);
			activations.Add(current);
		}
		return (current, activations);
	}

	private static float[] DenseLayer(float[] input, float[,] weights, float[] biases, bool applyRelu)
	{
		var outputSize = biases.Length;
		var output = new float[outputSize];
		for (var o = 0; o < outputSize; o++)
		{
			var sum = biases[o];
			for (var i = 0; i < input.Length; i++)
				sum += weights[o, i] * input[i];
			output[o] = applyRelu ? MathF.Max(0f, sum) : sum;
		}
		return output;
	}
}
