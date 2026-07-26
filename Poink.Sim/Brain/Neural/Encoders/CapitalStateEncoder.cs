// ── Encoder: bounded capital/position ratios ─────────────────────────────
//  Extracted from the block every brain duplicated identically (position
//  direction, unrealized/realized PnL over equity, cash over equity) -- no
//  dependency on knowing starting cash. Every composed brain includes this
//  regardless of how its market data is represented (see
//  RlTrainingService.TryBuildBrain, which appends it unconditionally rather
//  than exposing it as a pickable EncoderKind -- an agent that doesn't know
//  whether it already holds a position isn't a meaningful brain to build).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class CapitalStateEncoder : IObservationEncoder
{
    private const int Count = 4;

    private readonly IFeatureNormalizer _directionNormalizer;
    private readonly IFeatureNormalizer _equityRatioNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public CapitalStateEncoder(IFeatureNormalizer? directionNormalizer = null, IFeatureNormalizer? equityRatioNormalizer = null)
    {
        _directionNormalizer = directionNormalizer ?? new DirectionNormalizer();
        _equityRatioNormalizer = equityRatioNormalizer ?? new EquityRatioNormalizer();
        _features =
        [
            new FeatureDescriptor("position", _directionNormalizer),
            new FeatureDescriptor("unrealized/eq", _equityRatioNormalizer),
            new FeatureDescriptor("realized/eq", _equityRatioNormalizer),
            new FeatureDescriptor("cash/eq", _equityRatioNormalizer),
        ];
    }

    public int FeatureCount => Count;

    public int SequenceLength => 0;

    public int WarmupBars => 0; // account state is meaningful from the first tick -- no windowing

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var capital = observation.Capital;
        double direction = observation.Position is { } position ? (position.Direction == PositionDirection.Long ? 1 : -1) : 0;

        return
        [
            _directionNormalizer.Normalize(direction, observation),
            _equityRatioNormalizer.Normalize(capital.UnrealizedPnl, observation),
            _equityRatioNormalizer.Normalize(capital.RealizedPnl, observation),
            _equityRatioNormalizer.Normalize(capital.Cash, observation),
        ];
    }
}
