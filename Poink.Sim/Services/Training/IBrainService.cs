
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal interface IBrainService
{
    IReadOnlyList<BrainSummary> Brains { get; }
    string? Status { get; }

    event Action? OnChanged;

    bool CreateNetwork(string name, BrainRecipe recipe);
    bool UpdateNetwork(long id, string name, BrainRecipe recipe);
    bool DeleteNetwork(long id);
    void Refresh();
}
