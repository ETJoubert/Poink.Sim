using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

internal interface INeuralNetworkRepository
{
	Task<Guid> CreateAsync(string name, string structureJson, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<NeuralNetworkModel>> ListAsync(CancellationToken cancellationToken = default);

	Task<NeuralNetworkModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

	Task<bool> UpdateAsync(Guid id, string name, string structureJson, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
