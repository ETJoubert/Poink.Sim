// ── Shared, mutable source of truth for every registered brain ──────────
//  RlTrainingService and SimulationEngine each used to snapshot
//  IEnumerable<ISimulationBrain> into their own private list once at DI
//  construction -- fine while every brain was fixed at startup, but the
//  Train page's Brain Builder panel (see RlTrainingService.TryBuildBrain)
//  constructs new ComposedPolicyBrain instances at runtime, and both
//  services need to see those immediately. This singleton is seeded from the
//  same DI-registered ISimulationBrain collection (Program.cs) and adds
//  Register for anything built afterward -- All is read fresh on every call
//  rather than cached, so a brain registered mid-session is visible to
//  whichever service asks next.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain;

internal sealed class BrainRegistry
{
    private readonly object _lock = new();
    private readonly List<ISimulationBrain> _brains;

    public BrainRegistry(IEnumerable<ISimulationBrain> brains) => _brains = brains.ToList();

    public IReadOnlyList<ISimulationBrain> All
    {
        get { lock (_lock) { return _brains.ToList(); } }
    }

    // Adds a brain built at runtime (e.g. the Train page's Brain Builder) --
    // Name is the only identity key across RlTrainingService/SimulationEngine/
    // IModelRepository (a brain_name column), so a collision would silently
    // shadow lookups against whichever brain the caller actually meant.
    public void Register(ISimulationBrain brain)
    {
        lock (_lock)
        {
            if (_brains.Any(b => b.Name == brain.Name))
                throw new InvalidOperationException($"a brain named '{brain.Name}' already exists");
            _brains.Add(brain);
        }
    }
}
