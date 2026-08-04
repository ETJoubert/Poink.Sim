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
//
//  3 optional trailing-list features, each 0 length (absent) by default so
//  an old saved recipe still deserializes to today's fixed 12 fields --
//  TradeOutcomeList/DirectionList read observation.RecentTrades (Trader's
//  full closed-trade log, oldest-first), most-recent-last, zero-padded at
//  the front when fewer than the requested length exist yet (same "no false
//  signal" convention as BarWindowEncoder's own zero-padding). PositionLevel
//  instead reads price records (bars' Close, or ticks' mid) recorded since
//  the currently open position's own EntryTime -- flat (no position) or no
//  records yet since it opened both fall back to all-0, since there's no
//  entry price to compare against. All 3 share BinaryNormalizer (already-
//  computed 0/1 flags, nothing left to scale) -- same normalizer
//  BarWindowEncoder's own Binary mode uses.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TraderStateEncoder : IObservationEncoder
{
    private const int BaseCount = 12;

    private readonly int _tradeOutcomeListLength;
    private readonly int _positionLevelListLength;
    private readonly int _directionListLength;
    private readonly IFeatureNormalizer _directionNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IFeatureNormalizer _valueNormalizer;
    private readonly IFeatureNormalizer _binaryNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TraderStateEncoder(
        int tradeOutcomeListLength = 0, int positionLevelListLength = 0, int directionListLength = 0,
        IFeatureNormalizer? directionNormalizer = null, IFeatureNormalizer? priceNormalizer = null,
        IFeatureNormalizer? valueNormalizer = null, IFeatureNormalizer? binaryNormalizer = null)
    {
        _tradeOutcomeListLength = tradeOutcomeListLength;
        _positionLevelListLength = positionLevelListLength;
        _directionListLength = directionListLength;
        _directionNormalizer = directionNormalizer ?? new DirectionNormalizer();
        _priceNormalizer = priceNormalizer ?? new RawPriceNormalizer();
        _valueNormalizer = valueNormalizer ?? new RawValueNormalizer();
        _binaryNormalizer = binaryNormalizer ?? new BinaryNormalizer();

        var features = new List<FeatureDescriptor>(FeatureCount)
        {
            new("position", _directionNormalizer),
            new("position-price", _priceNormalizer),
            new("position-ticks", _valueNormalizer),
            new("position-distance", _priceNormalizer),
            new("current-price", _priceNormalizer),
            new("cash", _valueNormalizer),
            new("realized", _valueNormalizer),
            new("unrealized", _valueNormalizer),
            new("equity", _valueNormalizer),
            new("margin", _valueNormalizer),
            new("margin-level", _valueNormalizer),
            new("position-count", _valueNormalizer),
        };
        for (int i = _tradeOutcomeListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"trade-outcome-t-{i}", _binaryNormalizer));
        for (int i = _positionLevelListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"position-level-t-{i}", _binaryNormalizer));
        for (int i = _directionListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"direction-t-{i}", _binaryNormalizer));
        _features = features;
    }

    public int FeatureCount => BaseCount + _tradeOutcomeListLength + _positionLevelListLength + _directionListLength;

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

        var result = new List<float>(FeatureCount)
        {
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
        };
        result.AddRange(EncodeTradeOutcomes(observation));
        result.AddRange(EncodePositionLevels(observation));
        result.AddRange(EncodeDirections(observation));
        return result.ToArray();
    }

    // Last _tradeOutcomeListLength closed trades' win(1)/loss(0) -- RealizedPnl
    // > 0 is a win, <= 0 (including breakeven) a loss, matching the plain
    // binary split requested (no separate breakeven bucket, unlike
    // TradeStatistics' win/loss/neither split).
    private float[] EncodeTradeOutcomes(SimulationObservation observation) =>
        EncodeTrailingTrades(observation, _tradeOutcomeListLength, trade => trade.RealizedPnl > 0 ? 1 : 0);

    // Same trade history as EncodeTradeOutcomes, index-aligned -- entry i here
    // and entry i there both describe the i-th most recent closed trade.
    private float[] EncodeDirections(SimulationObservation observation) =>
        EncodeTrailingTrades(observation, _directionListLength, trade => trade.Direction == PositionDirection.Long ? 1 : 0);

    private float[] EncodeTrailingTrades(SimulationObservation observation, int length, System.Func<Trade, double> select)
    {
        var features = new float[length];
        if (length == 0) return features;

        var trades = observation.RecentTrades;
        int available = System.Math.Min(length, trades.Count);
        int padding = length - available;
        for (int i = 0; i < available; i++)
            features[padding + i] = _binaryNormalizer.Normalize(select(trades[trades.Count - available + i]), observation);
        return features;
    }

    // 1 if a price record (bar Close, or tick mid) since the open position's
    // own EntryTime is above its EntryPrice, 0 otherwise -- flat (no position)
    // stays all-0, same neutral fallback as position-price above.
    private float[] EncodePositionLevels(SimulationObservation observation)
    {
        var features = new float[_positionLevelListLength];
        if (_positionLevelListLength == 0 || observation.Position is not { } position) return features;

        bool useBars = observation.RecentBars.Count > 0;
        var pricesSinceOpen = useBars
            ? observation.RecentBars.Where(b => b.CloseTime >= position.EntryTime).Select(b => b.Close).ToList()
            : observation.RecentTicks.Where(t => t.Timestamp >= position.EntryTime).Select(t => (t.Bid + t.Ask) / 2.0).ToList();

        int available = System.Math.Min(_positionLevelListLength, pricesSinceOpen.Count);
        int padding = _positionLevelListLength - available;
        for (int i = 0; i < available; i++)
        {
            double price = pricesSinceOpen[pricesSinceOpen.Count - available + i];
            features[padding + i] = _binaryNormalizer.Normalize(price > position.EntryPrice ? 1 : 0, observation);
        }
        return features;
    }
}
