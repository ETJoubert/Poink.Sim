using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Turns NetworkOptionsModel plus the sizes Feature/Action assemblers produced into a trainable
/// network. Only MLP is implemented -- LSTM/CNN throw rather than silently building the wrong
/// architecture.
/// </summary>
internal sealed class NetworkAssembler
{
	public MlpNetwork Assemble(NetworkOptionsModel options, int inputSize, int outputSize, Random random)
	{
		if (options.ArchitectureKind != ArchitectureKind.Mlp)
			throw new NotSupportedException($"{options.ArchitectureKind} isn't implemented yet -- only Mlp is built so far.");

		var hiddenSizes = EffectiveHiddenSizes(options);
		var layerSizes = new List<int> { inputSize };
		layerSizes.AddRange(hiddenSizes);
		layerSizes.Add(outputSize);

		var weights = new List<float[,]>();
		var biases = new List<float[]>();
		for (var layer = 0; layer < layerSizes.Count - 1; layer++)
		{
			var (w, b) = InitializeLayer(layerSizes[layer], layerSizes[layer + 1], random);
			weights.Add(w);
			biases.Add(b);
		}

		return new MlpNetwork(inputSize, hiddenSizes, outputSize, weights, biases);
	}

	public static IReadOnlyList<int> EffectiveHiddenSizes(NetworkOptionsModel options) =>
		options.MlpHiddenSizes is { Count: > 0 } sizes ? sizes : [64];

	// He initialization for ReLU layers, reused for the linear output layer to avoid a second branch.
	private static (float[,] Weights, float[] Biases) InitializeLayer(int inputSize, int outputSize, Random random)
	{
		var scale = MathF.Sqrt(2f / inputSize);
		var weights = new float[outputSize, inputSize];
		for (var o = 0; o < outputSize; o++)
			for (var i = 0; i < inputSize; i++)
				weights[o, i] = (float)(NextGaussian(random) * scale);

		return (weights, new float[outputSize]);
	}

	// Box-Muller transform -- .NET's Random has no built-in Gaussian sample.
	private static double NextGaussian(Random random)
	{
		var u1 = 1.0 - random.NextDouble();
		var u2 = random.NextDouble();
		return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
	}
}
