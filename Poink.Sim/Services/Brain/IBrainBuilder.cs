
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal interface IBrainBuilder
{
    IDescribableBrain Build(string name, BrainRecipe recipe);
}
