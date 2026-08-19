
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TraderStateEncoder : IObservationEncoder
{
    private readonly int _tradeOutcomeListLength;
    private readonly int _positionLevelListLength;
    private readonly int _directionListLength;
    private readonly IFeatureNormalizer _directionNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IFeatureNormalizer _valueNormalizer;
    private readonly IFeatureNormalizer _binaryNormalizer;
    private readonly IReadOnlyList<BaseFieldDefinition> _baseFields;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    private sealed record BaseFieldDefinition(
        TraderStateFieldKind Kind, string Name, IFeatureNormalizer Normalizer, Func<SimulationObservation, double> Value);

    public TraderStateEncoder(
        IReadOnlyList<TraderStateFieldKind>? enabledBaseFields = null,
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

        var allBaseFields = BuildBaseFieldDefinitions();
        _baseFields = enabledBaseFields is null
            ? allBaseFields
            : allBaseFields.Where(f => enabledBaseFields.Contains(f.Kind)).ToList();

        var features = new List<FeatureDescriptor>();
        features.AddRange(_baseFields.Select(f => new FeatureDescriptor(f.Name, f.Normalizer)));
        for (int i = _tradeOutcomeListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"trade-outcome-t-{i}", _binaryNormalizer));
        for (int i = _positionLevelListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"position-level-t-{i}", _binaryNormalizer));
        for (int i = _directionListLength; i >= 1; i--)
            features.Add(new FeatureDescriptor($"direction-t-{i}", _binaryNormalizer));
        _features = features;
    }

    private List<BaseFieldDefinition> BuildBaseFieldDefinitions() => new()
    {
        new(TraderStateFieldKind.Position, "position", _directionNormalizer,
            o => o.Position is { } p ? (p.Direction == PositionDirection.Long ? 1 : -1) : 0),
        new(TraderStateFieldKind.PositionPrice, "position-price", _priceNormalizer,
            o => o.Position?.EntryPrice ?? 0),
        new(TraderStateFieldKind.PositionTicks, "position-ticks", _valueNormalizer,
            o => o.Position?.TicksHeld ?? 0),
        new(TraderStateFieldKind.PositionDistance, "position-distance", _priceNormalizer,
            o => o.Position?.CumulativeDistance ?? 0),
        new(TraderStateFieldKind.CurrentPrice, "current-price", _priceNormalizer,
            o => o.CurrentPrice),
        new(TraderStateFieldKind.Cash, "cash", _valueNormalizer,
            o => o.Capital.Cash),
        new(TraderStateFieldKind.Realized, "realized", _valueNormalizer,
            o => o.Capital.RealizedPnl),
        new(TraderStateFieldKind.Unrealized, "unrealized", _valueNormalizer,
            o => o.Capital.UnrealizedPnl),
        new(TraderStateFieldKind.Equity, "equity", _valueNormalizer,
            o => o.Capital.Equity),
        new(TraderStateFieldKind.Margin, "margin", _valueNormalizer,
            o => o.Capital.MarginUsed),
        new(TraderStateFieldKind.MarginLevel, "margin-level", _valueNormalizer,
            o => o.Capital.MarginLevel ?? 0),
        new(TraderStateFieldKind.PositionCount, "position-count", _valueNormalizer,
            o => o.Position is null ? 0 : 1),
    };

    public int FeatureCount => _baseFields.Count + _tradeOutcomeListLength + _positionLevelListLength + _directionListLength;

    public int SequenceLength => 0;

    public int WarmupBars => 0; // account state is meaningful from the first tick -- no windowing

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var result = new List<float>(FeatureCount);
        result.AddRange(_baseFields.Select(f => f.Normalizer.Normalize(f.Value(observation), observation)));
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
