// ── Reward policy: equity drift shaped by Volume/Time Profile structure ──
//  Same equity-drift base as EquityDriftRewardPolicy, with one additional
//  shaping term layered on top that only fires at entry (a flat -> held
//  transition since the previous decision, same detection
//  TradingRulesRewardPolicy uses) -- it rewards/penalizes a new position's
//  direction against where price sits relative to the current Volume Profile
//  (Poink.Sim.Models.VolumeProfileAnalysis) and Time Profile
//  (Poink.Sim.Models.TimeProfileAnalysis), the same market-structure data
//  VolumeProfileEncoder/TimeProfileEncoder already expose to a brain:
//
//   - Price outside the Value Area (above ValueAreaHigh or below
//     ValueAreaLow): a breakout regime -- the favored direction is
//     continuing away from the Value Area, only fully rewarded when recent
//     Bar Window momentum agrees (a breakout without momentum behind it is
//     the textbook "failed breakout" trap), half-credited when momentum is
//     flat, zero-credited when momentum actively opposes it.
//   - Price inside the Value Area: a rotation regime -- the favored
//     direction is toward the Point of Control (POC), the classic "POC
//     reversion" read; no momentum confirmation needed since a fade is by
//     definition a bet against the immediate move.
//   - Either way, the term's magnitude is scaled by how much the
//     Volume-Profile POC and Time-Profile POC agree (both computed over the
//     same trailing LookbackBars window) -- confluence between the two
//     (price both traded heavily AND was dwelled on) means a trustworthy,
//     "accepted" level, so the structural read is trusted at close to full
//     strength; divergence (heavy on one measure, thin on the other) means a
//     level price passed through quickly without matching participation
//     from the other side, so the read is discounted toward zero instead of
//     firing confidently on a level that was never really established.
//
//  LookbackBars here is this policy's own, independent setting -- it is NOT
//  tied to whatever LookbackBars a brain's IHasLookbackBars encoders (e.g.
//  VolumeProfileEncoder) are configured with. IRewardPolicy and the brain
//  are already orthogonal axes (see IRewardPolicy's doc comment); this file
//  doesn't change that, it just happens to default to the same starting
//  value (50) the encoders do.
//
//  Falls back to plain equity drift (no structure term) whenever there's no
//  bar history yet to read structure from -- RecentBarHistograms is empty in
//  raw-tick mode (see RewardContext's doc comment), and
//  VolumeProfileAnalysis/TimeProfileAnalysis.ComputeSummary both return null
//  on an empty/zero-volume window (e.g. still within warm-up) -- so this
//  never divides by a degenerate window, it just contributes nothing yet.
//
//  All constants are starting points, not tuned values -- same caveat
//  TradingRulesRewardPolicy's own doc comment carries.
// ─────────────────────────────────────────────────────────────────────────
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
