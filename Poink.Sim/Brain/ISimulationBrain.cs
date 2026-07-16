// ── Pluggable trading-decision contract ──────────────────────────────────
//  Deliberately NOT shaped like IForecastModel/IQuantileBrain/IPatternBrain
//  (no Build/BuildInput, no TorchSharp Module<Tensor,Tensor>) -- those exist
//  to construct/train a fresh model per run. A heuristic brain has no
//  weights and nothing to train. A future TRAINED brain can still implement
//  this exact interface: it would just hold a loaded Module<Tensor,Tensor>
//  as a private field and do its own tensor prep inside Decide -- this
//  interface only fixes the runtime observe/decide contract, not how (or
//  whether) a model is involved. SimulationDecision/SimulationObservation
//  live in Models/SimulationDecision.cs alongside the rest of the domain
//  data shapes.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal interface ISimulationBrain
{
    string Name { get; }

    SimulationDecision Decide(SimulationObservation observation);

    // Called once per run start; stateless brains no-op.
    void Reset();
}
