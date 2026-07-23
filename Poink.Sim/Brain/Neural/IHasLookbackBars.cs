// ── Optional brain capability: a configurable bar-count lookback ────────
//  Sibling to IPolicyGradientBrain, same "narrow interface, checked via is"
//  house style -- most brains have no such setting, so this isn't folded
//  into IPolicyGradientBrain itself. RlTrainingService sets this once, at
//  training start, from the Train page's form; a brain that owns it merges
//  down to this many bars itself (see VolumeProfilePolicyBrain.Encode)
//  rather than the runner/engine needing to know about it.
// ─────────────────────────────────────────────────────────────────────────
namespace Poink.Sim.Brain.Neural;

internal interface IHasLookbackBars
{
    int LookbackBars { get; set; }
}
