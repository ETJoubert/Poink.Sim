using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>Saves, lists, loads and deletes the training results kept for each trained model, along with the options each run used.</summary>
internal sealed class TrainingHistoryService
{
	private readonly TrainingHistoryRepository _repository;

	public TrainingHistoryService(TrainingHistoryRepository repository)
	{
		_repository = repository;
	}

	public Task<IReadOnlyList<TrainingHistorySummary>> ListAsync(Guid trainedModelId, CancellationToken cancellationToken = default) =>
		_repository.ListAsync(trainedModelId, cancellationToken);

	public Task<Guid> SaveAsync(
		Guid trainedModelId,
		string name,
		TrainingOptionsModel options,
		TrainingRunSnapshot run,
		CancellationToken cancellationToken = default
	)
	{
		var trimmedName = name.Trim();
		if (trimmedName.Length == 0)
			throw new ArgumentException("Training results need a name to be saved.", nameof(name));

		return _repository.CreateAsync(trainedModelId, trimmedName, JsonSerializer.Serialize(options), run, cancellationToken);
	}

	public Task<TrainingHistoryData?> LoadAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetAsync(id, cancellationToken);

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, cancellationToken);
}
