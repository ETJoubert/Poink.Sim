using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class CompositeEncoder : IObservationEncoder
{
	private readonly IReadOnlyList<IObservationEncoder> _parts;

	private CompositeEncoder(IReadOnlyList<IObservationEncoder> parts)
	{
		_parts = parts;
		FeatureCount = parts.Sum(p => p.FeatureCount);
		SequenceLength = parts.Sum(p => p.SequenceLength);
		Features = parts.SelectMany(p => p.Features).ToList();
	}

	public static CompositeEncoder Create(params IObservationEncoder[] parts)
	{
		if (parts.Length == 0)
			throw new InvalidOperationException("CompositeEncoder needs at least one part");

		for (int i = 1; i < parts.Length; i++)
			if (parts[i].SequenceLength > 0)
				throw new InvalidOperationException(
					$"{parts[i].GetType().Name} is sequence-bearing (SequenceLength={parts[i].SequenceLength}) but isn't listed "
						+ "first among CompositeEncoder's parts -- a recurrent architecture's leading-slice convention requires it first"
				);

		return new CompositeEncoder(parts);
	}

	public int FeatureCount { get; }

	public int SequenceLength { get; }

	public int WarmupBars => _parts.Count == 0 ? 0 : _parts.Max(p => p.WarmupBars);

	public IReadOnlyList<FeatureDescriptor> Features { get; }

	public float[] Encode(SimulationObservation observation)
	{
		var features = new float[FeatureCount];
		int offset = 0;
		foreach (var part in _parts)
		{
			var partFeatures = part.Encode(observation);
			Array.Copy(partFeatures, 0, features, offset, partFeatures.Length);
			offset += partFeatures.Length;
		}
		return features;
	}
}
