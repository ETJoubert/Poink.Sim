using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// The network's output shape and decoding rule, built from OutputOptionsModel. Continuous outputs
/// (Volume/StopLoss/TakeProfit) are one raw scalar each -- OutputOptionsModel has no levels/scaling
/// config, so there's nothing to decode; the engine reads them straight off the network's output.
/// </summary>
internal sealed record ActionSpace(
	IReadOnlyList<SimulationAction> Directions,
	bool IncludeVolume,
	bool IncludeStopLoss,
	bool IncludeTakeProfit,
	DecisionDecodingKind DecisionDecoding,
	double DecisionTemperature
)
{
	public int DirectionOutputWidth => Directions.Count;

	public int ContinuousOutputWidth => (IncludeVolume ? 1 : 0) + (IncludeStopLoss ? 1 : 0) + (IncludeTakeProfit ? 1 : 0);

	public int TotalOutputWidth => DirectionOutputWidth + ContinuousOutputWidth;

	public SimulationAction DecodeDirection(IReadOnlyList<float> rawDirectionOutputs, Random random)
	{
		if (rawDirectionOutputs.Count != Directions.Count)
			throw new ArgumentException(
				$"Expected {Directions.Count} raw direction outputs, got {rawDirectionOutputs.Count}.",
				nameof(rawDirectionOutputs)
			);

		if (DecisionDecoding == DecisionDecodingKind.HardMax)
			return Directions[ArgMax(rawDirectionOutputs)];

		var probabilities = Softmax(rawDirectionOutputs, DecisionTemperature);
		return Directions[Sample(probabilities, random)];
	}

	private static int ArgMax(IReadOnlyList<float> values)
	{
		var best = 0;
		for (var i = 1; i < values.Count; i++)
			if (values[i] > values[best])
				best = i;
		return best;
	}

	// Lower temperature sharpens the distribution toward ArgMax, higher flattens it toward
	// uniform; max-subtraction before exp() keeps this numerically stable.
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
