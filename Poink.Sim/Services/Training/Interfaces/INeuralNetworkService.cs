
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal interface INeuralNetworkService
{
    Task<Guid> CreateAsync(string name, string structureJson, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeuralNetworkModel>> ListAsync(CancellationToken cancellationToken = default);

    Task<NeuralNetworkModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Guid id, string name, string structureJson, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The JSON encoding a network's <see cref="BrainRecipe"/> is persisted under (its "structure" or "structure snapshot").</summary>
    string EncodeRecipe(BrainRecipe recipe);

    /// <summary>Decodes JSON produced by <see cref="EncodeRecipe"/>; falls back to an empty MLP recipe if it doesn't parse.</summary>
    BrainRecipe DecodeRecipe(string recipeJson);
}
