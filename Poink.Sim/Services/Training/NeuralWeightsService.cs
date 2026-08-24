
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal sealed class NeuralWeightsService : INeuralWeightsService
{
    private readonly INeuralWeightsRepository _repository;

    public NeuralWeightsService(INeuralWeightsRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(Guid neuralNetworkId, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(neuralNetworkId, name, data, weightCount, cancellationToken);

    public Task<IReadOnlyList<NeuralWeightsModel>> ListByNetworkAsync(Guid neuralNetworkId, CancellationToken cancellationToken = default) =>
        _repository.ListByNetworkAsync(neuralNetworkId, cancellationToken);

    public Task<NeuralWeightsModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(id, name, data, weightCount, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);
}
