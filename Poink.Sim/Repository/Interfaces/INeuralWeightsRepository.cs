
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

internal interface INeuralWeightsRepository
{
    Task<Guid> CreateAsync(Guid neuralNetworkId, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeuralWeightsModel>> ListByNetworkAsync(Guid neuralNetworkId, CancellationToken cancellationToken = default);

    Task<NeuralWeightsModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Guid id, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
