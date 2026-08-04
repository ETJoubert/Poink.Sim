
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal sealed class MarketStructureRewardPolicy : IRewardPolicy
{
    private const double ValueAreaFraction = 0.70; // matches VolumeProfileEncoder/TimeProfileEncoder's own hardcoded value

    private readonly double _structureBonusFraction;
    private readonly int _lookbackBars;
    private readonly int _momentumLookbackBars;

    public MarketStructureRewardPolicy(
        double structureBonusFraction = 0.001,
        int lookbackBars = 50,
        int momentumLookbackBars = 5)
    {
        _structureBonusFraction = structureBonusFraction;
        _lookbackBars = lookbackBars;
        _momentumLookbackBars = momentumLookbackBars;
    }

    public string Name => "Market Structure (value-area fade/breakout, POC confluence, momentum-confirmed)";

    public double ComputeReward(RewardContext context)
    {
        double reward = context.CapitalNow.Equity - context.PreviousCapital.Equity;

        if (context.PreviousPosition is null && context.Position is { } opened && context.RecentBarHistograms.Count > 0)
        {
            double bucketWidth = context.RecentBarHistograms[^1].BucketWidth;
            var volumeBuckets = BarPriceHistogram.Merge(context.RecentBarHistograms, _lookbackBars);
            var timeBuckets = BarPriceHistogram.MergeTime(context.RecentBars, context.RecentBarHistograms, _lookbackBars);

            var volumeSummary = VolumeProfileAnalysis.ComputeSummary(volumeBuckets, bucketWidth, ValueAreaFraction);
            var timeSummary = TimeProfileAnalysis.ComputeSummary(timeBuckets, bucketWidth, ValueAreaFraction);

            if (volumeSummary is { } volume && timeSummary is { } time)
            {
                PositionDirection favoredDirection;
                double confirmation;
                if (context.CurrentPrice > volume.ValueAreaHigh || context.CurrentPrice < volume.ValueAreaLow)
                {
                    favoredDirection = context.CurrentPrice > volume.ValueAreaHigh ? PositionDirection.Long : PositionDirection.Short;

                    int momentumStart = Math.Max(0, context.RecentBars.Count - _momentumLookbackBars);
                    double momentum = context.RecentBars.Count >= 2
                        ? context.RecentBars[^1].Close - context.RecentBars[momentumStart].Close
                        : 0;
                    int momentumSign = Math.Sign(momentum);
                    int favoredSign = favoredDirection == PositionDirection.Long ? 1 : -1;
                    confirmation = momentumSign == favoredSign ? 1.0 : (momentumSign == 0 ? 0.5 : 0.0);
                }
                else
                {
                    favoredDirection = context.CurrentPrice > volume.PointOfControl ? PositionDirection.Short : PositionDirection.Long;
                    confirmation = 1.0; // a fade is a bet against the immediate move -- no momentum confirmation to ask for
                }

                double convictionRange = Math.Max(volume.ValueAreaHigh - volume.ValueAreaLow, 1e-9);
                double conviction = Math.Clamp(1.0 - Math.Abs(volume.PointOfControl - time.PointOfControl) / convictionRange, 0.0, 1.0);

                double agreement = opened.Direction == favoredDirection ? 1.0 : -1.0;
                reward += agreement * conviction * confirmation * _structureBonusFraction * context.CapitalNow.Equity;
            }
        }

        return reward;
    }
}
