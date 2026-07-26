// ── Encoder: exact trader/account state ──────────────────────────────────
//  Extracted from the block every brain duplicated identically (position
//  direction plus every account-state figure Trader tracks) -- feeds each
//  value exactly as Trader/CapitalState report it (RawValueNormalizer/
//  RawPriceNormalizer passthrough), not scaled against equity, so the
//  network sees the trader's real position/cash/margin state directly.
//  Every composed brain includes this regardless of how its market data is
//  represented (see RlTrainingService.TryBuildBrain, which appends it
//  unconditionally rather than exposing it as a pickable EncoderKind -- an
//  agent that doesn't know whether it already holds a position isn't a
//  meaningful brain to build).
//
//  position-price is the open position's exact EntryPrice (0 while flat) --
//  same reasoning as VolumeProfileRawEncoder pairing each bucket's price
//  with its volume: knowing *that* a position is held (position/
//  position-count) without knowing the exact price it was entered at gives
//  the network no way to tell how far current-price has moved from entry,
//  only the size-conflated UnrealizedPnl figure.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TraderStateEncoder : IObservationEncoder
{
    private const int Count = 10;

    private readonly IFeatureNormalizer _directionNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IFeatureNormalizer _valueNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TraderStateEncoder(
        IFeatureNormalizer? directionNormalizer = null,
        IFeatureNormalizer? priceNormalizer = null,
        IFeatureNormalizer? valueNormalizer = null)
    {
        _directionNormalizer = directionNormalizer ?? new DirectionNormalizer();
        _priceNormalizer = priceNormalizer ?? new RawPriceNormalizer();
        _valueNormalizer = valueNormalizer ?? new RawValueNormalizer();
        _features =
        [
            new FeatureDescriptor("position", _directionNormalizer),
            new FeatureDescriptor("position-price", _priceNormalizer),
            new FeatureDescriptor("current-price", _priceNormalizer),
            new FeatureDescriptor("cash", _valueNormalizer),
            new FeatureDescriptor("realized", _valueNormalizer),
            new FeatureDescriptor("unrealized", _valueNormalizer),
            new FeatureDescriptor("equity", _valueNormalizer),
            new FeatureDescriptor("margin", _valueNormalizer),
            new FeatureDescriptor("margin-level", _valueNormalizer),
            new FeatureDescriptor("position-count", _valueNormalizer),
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
        double positionPrice = observation.Position?.EntryPrice ?? 0;
        double positionCount = observation.Position is null ? 0 : 1;

        return
        [
            _directionNormalizer.Normalize(direction, observation),
            _priceNormalizer.Normalize(positionPrice, observation),
            _priceNormalizer.Normalize(observation.CurrentPrice, observation),
            _valueNormalizer.Normalize(capital.Cash, observation),
            _valueNormalizer.Normalize(capital.RealizedPnl, observation),
            _valueNormalizer.Normalize(capital.UnrealizedPnl, observation),
            _valueNormalizer.Normalize(capital.Equity, observation),
            _valueNormalizer.Normalize(capital.MarginUsed, observation),
            _valueNormalizer.Normalize(capital.MarginLevel ?? 0, observation),
            _valueNormalizer.Normalize(positionCount, observation),
        ];
    }
}
