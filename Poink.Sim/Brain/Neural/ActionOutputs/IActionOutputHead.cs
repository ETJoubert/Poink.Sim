// ── Composable brain capability: one extra (non-direction) decision ─────
//  Sibling to IObservationEncoder/INetworkArchitecture -- a brain opts into
//  zero or more of these (see BrainCatalog.CreateActionOutputHead) to have
//  its network decide Volume/StopLoss/TakeProfit alongside the fixed
//  Hold/EnterLong/EnterShort/Close direction pick, instead of
//  ComposedPolicyBrain's hardcoded CapitalFraction=0.5/no-SL-TP fallback.
//  Each head owns a fixed slice of the network's raw output layer
//  (OutputWidth) and everything about turning that slice into a value --
//  today's only implementation (DiscretizedLevelsActionHead) treats it as
//  logits over a small fixed set of levels, sampled the same categorical
//  way the direction pick is; a future continuous implementation could
//  instead treat its slice as e.g. a (mean, log_std) pair for a Gaussian
//  policy -- ComposedPolicyBrain never needs to know which.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Brain.Neural;

internal interface IActionOutputHead
{
    ActionOutputKind Kind { get; }

    // How many of the network's raw output slots this head consumes --
    // fixed at construction (before the network is built), since
    // ComposedPolicyBrain.Create needs the total extra width up front to
    // size the final layer.
    int OutputWidth { get; }

    // Decodes this head's slice of one forward pass's raw output logits
    // (shape [1, OutputWidth]) into a real-unit value (capital fraction
    // 0..1 for Volume, percent-distance-from-price for StopLoss/
    // TakeProfit; null means "don't set it"), that decision's log-
    // probability (summed into the same combined ActionLogProbability the
    // direction pick contributes to, for the training strategy's REINFORCE
    // loss), and a labeled breakdown for BrainIoPanel's OUTPUTS card.
    (double? Value, Tensor LogProbability, IReadOnlyList<RlOutputFeature> Outputs) Sample(Tensor rawSlice);
}
