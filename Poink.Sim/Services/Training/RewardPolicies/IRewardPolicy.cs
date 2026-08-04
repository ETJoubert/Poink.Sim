
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal readonly record struct RewardContext(
    CapitalState PreviousCapital, CapitalState CapitalNow, Position? PreviousPosition, Position? Position, DateTime TimestampUtc,
    double CurrentPrice, IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<BarPriceHistogram> RecentBarHistograms);

internal interface IRewardPolicy
{
    string Name { get; }


    void OnEpisodeStart() { }

    double ComputeReward(RewardContext context);
}
