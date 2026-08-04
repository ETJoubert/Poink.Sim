
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;


internal sealed record RlStep(SimulationAction Action, Tensor? LogProbability, double Reward, Tensor? Entropy);


internal readonly record struct RlBarAction(
    SimulationAction Action, double? EntryPrice, bool? PositionIsLong, double? StopLossPrice, double? TakeProfitPrice,
    bool Blocked = false);


internal sealed record RlEpisode(
    IReadOnlyList<RlStep> Steps, CapitalState FinalCapital, IReadOnlyList<RlInputFeature>? LastInputs,
    IReadOnlyList<RlOutputFeature>? LastOutputs, IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown,
    DateTime WindowStart, DateTime WindowEnd, IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> BarHistograms,
    IReadOnlyList<IReadOnlyList<RlInputFeature>> BarInputs, IReadOnlyList<double> BarEquity,
    IReadOnlyList<IReadOnlyList<RlOutputFeature>> BarOutputs, int WarmupBars, IReadOnlyList<RlBarAction> BarActions);

internal sealed record RlEpisodeTrades(
    int EpisodeNumber, IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown,
    int BlockedLongs, int BlockedShorts, int BlockedCloses);


public sealed record RlEpisodeMetric(int EpisodeNumber, double Reward, double FinalEquity);

public static class RlEpisodeMetricStats
{
    public static double Median(IReadOnlyList<RlEpisodeMetric> episodes)
    {
        var sorted = episodes.Select(e => e.Reward).OrderBy(r => r).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    public static double StdDev(IReadOnlyList<RlEpisodeMetric> episodes)
    {
        var rewards = episodes.Select(e => e.Reward).ToList();
        double mean = rewards.Average();
        double variance = rewards.Sum(r => (r - mean) * (r - mean)) / rewards.Count;
        return Math.Sqrt(variance);
    }

    public static double WinRate(IReadOnlyList<RlEpisodeMetric> episodes) =>
        episodes.Count(e => e.Reward > 0) * 100.0 / episodes.Count;
}


public sealed record RlEpisodeWindow(int EpisodeNumber, DateTime Start, DateTime End);

internal sealed record RlActionCounts(int Hold, int EnterLong, int EnterShort, int Close)
{
    public static readonly RlActionCounts Empty = new(0, 0, 0, 0);

    public RlActionCounts Add(SimulationAction action) => action switch
    {
        SimulationAction.Hold => this with { Hold = Hold + 1 },
        SimulationAction.EnterLong => this with { EnterLong = EnterLong + 1 },
        SimulationAction.EnterShort => this with { EnterShort = EnterShort + 1 },
        SimulationAction.Close => this with { Close = Close + 1 },
        _ => this,
    };

    public int Total => Hold + EnterLong + EnterShort + Close;
}
