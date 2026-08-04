// ── Encoder: exact trader/account state ──────────────────────────────────
//  Extracted from the block every brain duplicated identically (position
//  direction plus every account-state figure Trader tracks) -- feeds each
//  value exactly as Trader/CapitalState report it (RawValueNormalizer/
//  RawPriceNormalizer passthrough), not scaled against equity, so the
//  network sees the trader's real position/cash/margin state directly.
//  A pickable EncoderKind like any other (see BrainCatalog.CreateEncoder) --
//  BrainBuilder defaults it unchecked (opt-in), so a brain only sees whether
//  it already holds a position if that's deliberately selected.
//
//  position-price is the open position's exact EntryPrice (0 while flat) --
//  same reasoning as VolumeProfileEncoder's Raw mode pairing each bucket's
//  price with its volume: knowing *that* a position is held (position/
//  position-count) without knowing the exact price it was entered at gives
//  the network no way to tell how far current-price has moved from entry,
//  only the size-conflated UnrealizedPnl figure.
//
//  position-ticks/position-distance are Position.TicksHeld/CumulativeDistance
//  (0 while flat) -- running stats Trader.Advance grows every raw tick a
//  position stays open (see Position's doc comment). Distance is path length
//  (sum of |tick-to-tick mid-price delta|), not net EntryPrice-to-current
//  displacement -- already derivable from position-price/current-price
//  above -- so it tells the network how much a position has been whipsawed,
//  not just where it ended up.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TraderStateEncoder : IObservationEncoder
{
    private const int Count = 12;

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
            new FeatureDescriptor("position-ticks", _valueNormalizer),
            new FeatureDescriptor("position-distance", _priceNormalizer),
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
        double positionTicks = observation.Position?.TicksHeld ?? 0;
        double positionDistance = observation.Position?.CumulativeDistance ?? 0;
        double positionCount = observation.Position is null ? 0 : 1;

        return
        [
            _directionNormalizer.Normalize(direction, observation),
            _priceNormalizer.Normalize(positionPrice, observation),
            _valueNormalizer.Normalize(positionTicks, observation),
            _priceNormalizer.Normalize(positionDistance, observation),
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
