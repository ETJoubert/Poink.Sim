// ── CNN-only: exposes an ordered sub-array a Conv1d kernel can sweep ────
//  Independent of IObservationEncoder.SequenceLength -- that contract is
//  owned by CompositeEncoder's ordering guard/LstmArchitecture, which both
//  require the sequence-bearing part to sit at parts[0] (see
//  CompositeEncoder's doc comment). This interface lets CnnArchitecture pull
//  its kernel's sequence from any part in the composite instead, e.g.
//  VolumeProfileEncoder's price-ordered histogram buckets, without touching
//  that existing BarWindow-at-offset-0 contract LSTM still depends on.
//  ComposedPolicyBrain.Create looks for the first part implementing this
//  interface when a Cnn-architecture brain's BrainRecipe.CnnKernelSweepSource
//  asks for something other than BarWindow.
// ─────────────────────────────────────────────────────────────────────────
namespace Poink.Sim.Brain.Neural;

internal interface ICnnSweepable
{
    // How many of this encoder's own leading FeatureCount slots form the
    // ordered sequence a CNN kernel sweeps over -- not necessarily equal to
    // FeatureCount (e.g. VolumeProfileEncoder's trailing POC/value-area
    // summary scalars sit outside it).
    int SweepLength { get; }
}
