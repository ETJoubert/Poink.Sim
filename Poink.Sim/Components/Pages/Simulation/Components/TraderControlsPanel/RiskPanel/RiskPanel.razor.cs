using System;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Simulation
{

    public partial class RiskPanel : ComponentBase
    {
        [Parameter] public bool IsLong { get; set; } = true;
        [Parameter] public bool IsMarketOrder { get; set; } = true;

        [Parameter] public double Cash { get; set; }
        [Parameter] public double CapitalFraction { get; set; }
        [Parameter] public double Leverage { get; set; } = 1.0;
        [Parameter] public double PipSize { get; set; } = 0.01;

        [Parameter] public double? EntryPrice { get; set; }
        [Parameter] public double? StopLossPips { get; set; }
        [Parameter] public double? TakeProfitPips { get; set; }
        [Parameter] public double StopOutLevelPercent { get; set; } = 50.0;

        private PositionDirection Direction => IsLong ? PositionDirection.Long : PositionDirection.Short;

        private bool HasEntry => EntryPrice is { } entry && entry > 0;

        private double? StopLossPrice =>
            StopLossPips is { } pips && HasEntry
                ? RiskHelper.StopLossPriceFromPips(Direction, EntryPrice!.Value, pips, PipSize)
                : null;

        private double? TakeProfitPrice =>
            TakeProfitPips is { } pips && HasEntry
                ? RiskHelper.TakeProfitPriceFromPips(Direction, EntryPrice!.Value, pips, PipSize)
                : null;

        private double? Volume =>
            HasEntry ? RiskHelper.PositionSize(Cash, CapitalFraction, Leverage, EntryPrice!.Value) : null;

        private double? Notional =>
            Volume is { } size && EntryPrice is { } entry ? size * entry : null;

        private double MarginUsed => Math.Clamp(CapitalFraction, 0, 1) * Cash;

        private double? StopRisk =>
            StopLossPrice is { } stop && HasEntry
                ? RiskHelper.ProjectedRiskOnStop(Direction, Cash, CapitalFraction, Leverage, EntryPrice!.Value, stop)
                : null;

        private double? TakeProfitReward =>
            TakeProfitPrice is { } target && HasEntry
                ? -RiskHelper.ProjectedRiskOnStop(Direction, Cash, CapitalFraction, Leverage, EntryPrice!.Value, target)
                : null;

        private double? RewardToRisk =>
            StopRisk is { } risk && risk > 0 && TakeProfitReward is { } reward && reward > 0
                ? reward / risk
                : null;

        private double MaxRisk =>
            RiskHelper.MaxRiskAtStopOut(Direction, Cash, CapitalFraction, Leverage, StopOutLevelPercent);

        private double RiskFractionOf(double risk) => RiskHelper.RiskFraction(risk, Cash);
    }
}
