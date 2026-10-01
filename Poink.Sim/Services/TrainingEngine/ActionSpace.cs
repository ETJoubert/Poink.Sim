using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// The network's output shape and decoding rule, built from OutputOptionsModel. Position size, stop
/// and target come from Position Control, so the network only outputs a direction.
/// </summary>
internal sealed record ActionSpace(
	IReadOnlyList<SimulationAction> Directions,
	DecisionDecodingKind DecisionDecoding,
	double DecisionTemperature
)
{
	public int DirectionOutputWidth => Directions.Count;

	public int TotalOutputWidth => DirectionOutputWidth;

	public SimulationAction DecodeDirection(IReadOnlyList<float> rawDirectionOutputs, Random random)
	{
		if (rawDirectionOutputs.Count != Directions.Count)
			throw new ArgumentException(
				$"Expected {Directions.Count} raw direction outputs, got {rawDirectionOutputs.Count}.",
				nameof(rawDirectionOutputs)
			);

		if (DecisionDecoding == DecisionDecodingKind.HardMax)
			return Directions[ArgMax(rawDirectionOutputs)];

		return Directions[Sample(Probabilities(rawDirectionOutputs), random)];
	}

	private static int ArgMax(IReadOnlyList<float> values)
	{
		var best = 0;
		for (var i = 1; i < values.Count; i++)
			if (values[i] > values[best])
				best = i;
		return best;
	}

	// Also used by the policy-gradient update to weigh each action's contribution to the loss.
	public double[] Probabilities(IReadOnlyList<float> rawDirectionOutputs) => Softmax(rawDirectionOutputs, DecisionTemperature);

	private static double[] Softmax(IReadOnlyList<float> values, double temperature)
	{
		var scale = Math.Max(temperature, 1e-8);
		var scaled = values.Select(v => v / scale).ToArray();
		var max = scaled.Max();
		var exp = scaled.Select(v => Math.Exp(v - max)).ToArray();
		var sum = exp.Sum();
		return exp.Select(v => v / sum).ToArray();
	}

	private static int Sample(IReadOnlyList<double> probabilities, Random random)
	{
		var roll = random.NextDouble();
		var cumulative = 0.0;
		for (var i = 0; i < probabilities.Count; i++)
		{
			cumulative += probabilities[i];
			if (roll <= cumulative)
				return i;
		}
		return probabilities.Count - 1;
	}
}
