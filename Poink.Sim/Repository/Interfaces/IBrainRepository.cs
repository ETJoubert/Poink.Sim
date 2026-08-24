
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Repository;


public sealed record BrainSummary(long Id, string Name, BrainRecipe Recipe, DateTime SavedAt, DateTime UpdatedAt);

internal interface IBrainRepository
{
    long SaveBrain(string name, BrainRecipe recipe);

    List<BrainSummary> ListBrains();

    void UpdateBrain(long id, string name, BrainRecipe recipe);

    void DeleteBrain(long id);
}
