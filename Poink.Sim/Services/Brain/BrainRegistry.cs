
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain;

/// <summary>
/// Holds every DI-registered <see cref="ISimulationBrain"/> instance so callers can list or
/// look one up by name without depending on the DI container directly.
/// </summary>
internal sealed class BrainRegistry
{
    private readonly object _lock = new();
    private readonly List<ISimulationBrain> _brains;

    public BrainRegistry(IEnumerable<ISimulationBrain> brains) => _brains = brains.ToList();

    public IReadOnlyList<ISimulationBrain> All
    {
        get { lock (_lock) { return _brains.ToList(); } }
    }

    public void Register(ISimulationBrain brain)
    {
        lock (_lock)
        {
            if (_brains.Any(b => b.Name == brain.Name))
                throw new InvalidOperationException($"a brain named '{brain.Name}' already exists");
            _brains.Add(brain);
        }
    }

    public void Unregister(string name)
    {
        lock (_lock)
        { _brains.RemoveAll(b => b.Name == name); }
    }
}
