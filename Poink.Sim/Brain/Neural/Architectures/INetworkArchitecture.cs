// ── Composable brain capability: feature vector -> action logits ────────
//  Sibling to IObservationEncoder -- the other half of what used to be baked
//  directly into each concrete brain's constructor (see e.g. the now-deleted
//  MlpPolicyBrain/LstmPolicyBrain). ComposedPolicyBrain pairs one of these
//  with one IObservationEncoder to build a brain's Network.
//
//  Build takes featureCount/sequenceLength rather than just an
//  IObservationEncoder so an architecture never needs to depend on the
//  Brain.Neural encoder types themselves -- a flat architecture (see
//  MlpArchitecture) ignores sequenceLength entirely, while a recurrent one
//  (see LstmArchitecture) uses it to narrow() the leading sequence-bearing
//  slice out of the flat feature vector (see IObservationEncoder's doc
//  comment on that convention).
// ─────────────────────────────────────────────────────────────────────────
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal interface INetworkArchitecture
{
    // Short display tag (e.g. "MLP-32-16") -- not consumed by any UI today,
    // included for when a brain's Name wants to summarize its architecture.
    string Name { get; }

    // outputWidth: the final layer's total width -- however many directions
    // the brain was built with (BrainCatalog.ComposedPolicyBrain.AllDirections
    // or a narrower subset, see SimulationAction's doc comment) plus the
    // combined OutputWidth of every IActionOutputHead it was built with.
    // ComposedPolicyBrain slices its own direction count off the front and
    // hands each head its own trailing slice, in that order.
    Module<Tensor, Tensor> Build(int featureCount, int sequenceLength, int outputWidth);
}
