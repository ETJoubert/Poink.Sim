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

    private static readonly IReadOnlyList<string> Labels =
        ["position", "unrealized/eq", "realized/eq", "cash/eq"];

    public int FeatureCount => Count;

    public int SequenceLength => 0;

    public int WarmupBars => 0; // account state is meaningful from the first tick -- no windowing

    public IReadOnlyList<string> FeatureLabels => Labels;

    public float[] Encode(SimulationObservation observation)
    {
        var capital = observation.Capital;
        double equity = capital.Equity;

        return
        [
            observation.Position is { } position ? (position.Direction == PositionDirection.Long ? 1f : -1f) : 0f,
            equity != 0 ? (float)(capital.UnrealizedPnl / equity) : 0f,
            equity != 0 ? (float)(capital.RealizedPnl / equity) : 0f,
            equity != 0 ? (float)(capital.Cash / equity) : 0f,
        ];
    }
}
