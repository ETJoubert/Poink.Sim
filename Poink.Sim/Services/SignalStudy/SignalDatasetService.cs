using System.Collections.Generic;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.SignalStudy;

internal sealed class SignalDatasetService : ISignalDatasetService
{
	private readonly ISignalDatasetRepository _repository;

	public SignalDatasetService(ISignalDatasetRepository repository) => _repository = repository;

	public Task<IReadOnlyList<SignalDatasetInfo>> ListAsync() => Task.Run(_repository.ListDatasets);

	public Task DeleteAsync(long datasetId) => Task.Run(() => _repository.DeleteDataset(datasetId));
}
