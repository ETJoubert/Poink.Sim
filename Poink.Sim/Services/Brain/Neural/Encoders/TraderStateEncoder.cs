
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

    private float[] EncodeTradeOutcomes(SimulationObservation observation) =>
        EncodeTrailingTrades(observation, _tradeOutcomeListLength, trade => trade.RealizedPnl > 0 ? 1 : 0);


    private float[] EncodeDirections(SimulationObservation observation) =>
        EncodeTrailingTrades(observation, _directionListLength, trade => trade.Direction == PositionDirection.Long ? 1 : 0);

    private float[] EncodeTrailingTrades(SimulationObservation observation, int length, System.Func<Trade, double> select)
    {
        var features = new float[length];
        if (length == 0)
            return features;

        var trades = observation.RecentTrades;
        int available = System.Math.Min(length, trades.Count);
        int padding = length - available;
        for (int i = 0; i < available; i++)
            features[padding + i] = _binaryNormalizer.Normalize(select(trades[trades.Count - available + i]), observation);
        return features;
    }

    private float[] EncodePositionLevels(SimulationObservation observation)
    {
        var features = new float[_positionLevelListLength];
        if (_positionLevelListLength == 0 || observation.Position is not { } position)
            return features;

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
