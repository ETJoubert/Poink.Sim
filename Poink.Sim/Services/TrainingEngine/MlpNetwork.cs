using System;
using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// A fully-connected feed-forward network: Dense(input -> hidden[0]) -ReLU-> ... -> Dense(hidden[^1]
/// -> output), output layer linear (ActionSpace decodes it downstream). Weights/biases are mutable
/// so the training loop can update them in place; this class only does the forward pass, no gradients.
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

	public float[] Forward(IReadOnlyList<float> input)
	{
		if (input.Count != InputSize)
			throw new ArgumentException($"Expected {InputSize} inputs, got {input.Count}.", nameof(input));

		var activations = new float[input.Count];
		for (var i = 0; i < input.Count; i++)
			activations[i] = input[i];

		for (var layer = 0; layer < Weights.Count; layer++)
		{
			var isOutputLayer = layer == Weights.Count - 1;
			activations = DenseLayer(activations, Weights[layer], Biases[layer], applyRelu: !isOutputLayer);
		}
		return activations;
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
