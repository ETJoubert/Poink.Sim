using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Data.Entities;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Repository;

/// <summary>Stores trained models for the Training Engine. Saving under an existing name overwrites that model in place, keeping its id and history.</summary>
internal sealed class TrainedModelRepository
{
	private readonly IDbContextFactory<PoinkSimContext> _contextFactory;
	private readonly ILogger<TrainedModelRepository> _logger;

	public TrainedModelRepository(IDbContextFactory<PoinkSimContext> contextFactory, ILogger<TrainedModelRepository> logger)
	{
		_contextFactory = contextFactory;
		_logger = logger;
	}

	public async Task<Guid> SaveAsync(
		string name,
		string configJson,
		byte[] weights,
		long weightCount,
		int episodesTrained,
		CancellationToken cancellationToken = default
	)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = await context.TrainedModels.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);
		var overwrote = entity is not null;
		if (entity is null)
		{
			entity = new TrainedModel { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
			context.TrainedModels.Add(entity);
		}
		else
			entity.UpdatedAt = DateTime.UtcNow;

		entity.ConfigJson = configJson;
		entity.Weights = weights;
		entity.WeightCount = weightCount;
		entity.EpisodesTrained = episodesTrained;
		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Saved trained model '{Name}' as id {Id} (overwrote existing: {Overwrote}, episodes {EpisodesTrained}, weights {WeightCount})",
			name,
			entity.Id,
			overwrote,
			episodesTrained,
			weightCount
		);
		return entity.Id;
	}

	public async Task<IReadOnlyList<TrainedModelSummary>> ListAsync(CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var summaries = await context
			.TrainedModels.OrderBy(m => m.Name)
			.Select(m => new TrainedModelSummary(m.Id, m.Name, m.EpisodesTrained, m.WeightCount, m.CreatedAt, m.UpdatedAt))
			.ToListAsync(cancellationToken);

		_logger.LogInformation("Listed {ModelCount} trained models", summaries.Count);
		return summaries;
	}

	public async Task<TrainedModelData?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = await context.TrainedModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
		if (entity is null)
		{
			_logger.LogWarning("Trained model {Id} was not found", id);
			return null;
		}

		_logger.LogInformation("Loaded trained model '{Name}' (id {Id})", entity.Name, id);
		return new TrainedModelData(
			new TrainedModelSummary(entity.Id, entity.Name, entity.EpisodesTrained, entity.WeightCount, entity.CreatedAt, entity.UpdatedAt),
			entity.ConfigJson,
			entity.Weights
		);
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = await context.TrainedModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
		if (entity is null)
		{
			_logger.LogWarning("Trained model {Id} could not be deleted: not found", id);
			return false;
		}

		context.TrainedModels.Remove(entity);
		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Deleted trained model '{Name}' (id {Id}) along with its history", entity.Name, id);
		return true;
	}
}
