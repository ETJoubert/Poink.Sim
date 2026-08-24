
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal sealed class NeuralNetworkService : INeuralNetworkService
{
    private static readonly JsonSerializerOptions RecipeJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly INeuralNetworkRepository _repository;

    public NeuralNetworkService(INeuralNetworkRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(string name, string structureJson, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(name, structureJson, cancellationToken);

    public Task<IReadOnlyList<NeuralNetworkModel>> ListAsync(CancellationToken cancellationToken = default) =>
        _repository.ListAsync(cancellationToken);

    public Task<NeuralNetworkModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, string name, string structureJson, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(id, name, structureJson, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public string EncodeRecipe(BrainRecipe recipe) => JsonSerializer.Serialize(recipe, RecipeJsonOptions);

    public BrainRecipe DecodeRecipe(string recipeJson) =>
        JsonSerializer.Deserialize<BrainRecipe>(recipeJson, RecipeJsonOptions) ?? new BrainRecipe([], ArchitectureKind.Mlp, []);
}
