using System;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Exponentially weighted mean/variance of the rewards seen so far in a run. Trade Outcome learning
/// can't use the episode's own mean/std as its baseline because it updates before the episode ends.
/// </summary>
internal sealed class RewardBaseline
{
	private const double Decay = 0.01;
	private const int MinSamplesForAdvantage = 2;

	private int _count;
	private double _mean;
	private double _variance;

	public double Advantage(double reward, double maxMagnitude) =>
		_count < MinSamplesForAdvantage
			? 0
			: Math.Clamp((reward - _mean) / (Math.Sqrt(_variance) + 1e-8), -maxMagnitude, maxMagnitude);

	public void Add(double reward)
	{
		if (_count == 0)
		{
			_mean = reward;
			_variance = 0;
		}
		else
		{
			var difference = reward - _mean;
			_mean += Decay * difference;
			_variance = (1 - Decay) * (_variance + Decay * difference * difference);
		}
		_count++;
	}
}
