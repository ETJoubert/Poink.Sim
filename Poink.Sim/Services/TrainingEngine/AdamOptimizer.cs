using System;
using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Adam weight updates for one network: per-weight running averages of the gradient and its square
/// scale each step, so the learning rate means the same thing whatever the gradient's size. State is
/// per instance, so each network being trained needs its own.
/// </summary>
internal sealed class AdamOptimizer
{
	private const double FirstMomentDecay = 0.9;
	private const double SecondMomentDecay = 0.999;
	private const double Epsilon = 1e-8;

	private readonly List<float[,]> _weightMeans = [];
	private readonly List<float[,]> _weightSquares = [];
	private readonly List<float[]> _biasMeans = [];
	private readonly List<float[]> _biasSquares = [];

	private int _step;

	public AdamOptimizer(MlpNetwork network)
	{
		foreach (var weights in network.Weights)
		{
			_weightMeans.Add(new float[weights.GetLength(0), weights.GetLength(1)]);
			_weightSquares.Add(new float[weights.GetLength(0), weights.GetLength(1)]);
		}
		foreach (var biases in network.Biases)
		{
			_biasMeans.Add(new float[biases.Length]);
			_biasSquares.Add(new float[biases.Length]);
		}
	}

	public void Apply(MlpNetwork network, IReadOnlyList<float[,]> weightGradients, IReadOnlyList<float[]> biasGradients, double learningRate)
	{
		_step++;
		var meanCorrection = 1 - Math.Pow(FirstMomentDecay, _step);
		var squareCorrection = 1 - Math.Pow(SecondMomentDecay, _step);

		for (var layer = 0; layer < network.Weights.Count; layer++)
		{
			var weights = network.Weights[layer];
			for (var o = 0; o < weights.GetLength(0); o++)
				for (var i = 0; i < weights.GetLength(1); i++)
					weights[o, i] -= Delta(_weightMeans[layer], _weightSquares[layer], o, i, weightGradients[layer][o, i], learningRate, meanCorrection, squareCorrection);

			var biases = network.Biases[layer];
			for (var o = 0; o < biases.Length; o++)
				biases[o] -= Delta(_biasMeans[layer], _biasSquares[layer], o, biasGradients[layer][o], learningRate, meanCorrection, squareCorrection);
		}
	}

	private static float Delta(float[,] means, float[,] squares, int o, int i, float gradient, double learningRate, double meanCorrection, double squareCorrection)
	{
		means[o, i] = (float)(FirstMomentDecay * means[o, i] + (1 - FirstMomentDecay) * gradient);
		squares[o, i] = (float)(SecondMomentDecay * squares[o, i] + (1 - SecondMomentDecay) * gradient * gradient);
		return Step(means[o, i], squares[o, i], learningRate, meanCorrection, squareCorrection);
	}

	private static float Delta(float[] means, float[] squares, int o, float gradient, double learningRate, double meanCorrection, double squareCorrection)
	{
		means[o] = (float)(FirstMomentDecay * means[o] + (1 - FirstMomentDecay) * gradient);
		squares[o] = (float)(SecondMomentDecay * squares[o] + (1 - SecondMomentDecay) * gradient * gradient);
		return Step(means[o], squares[o], learningRate, meanCorrection, squareCorrection);
	}

	private static float Step(float mean, float square, double learningRate, double meanCorrection, double squareCorrection) =>
		(float)(learningRate * (mean / meanCorrection) / (Math.Sqrt(square / squareCorrection) + Epsilon));
}
