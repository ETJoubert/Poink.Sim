using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Data.Entities;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Training;

namespace Poink.Sim.Repository;

/// <summary>Stores the saved training results that belong to each trained model.</summary>
internal sealed class TrainingHistoryRepository
{
	private readonly IDbContextFactory<PoinkSimContext> _contextFactory;
	private readonly ILogger<TrainingHistoryRepository> _logger;

	public TrainingHistoryRepository(IDbContextFactory<PoinkSimContext> contextFactory, ILogger<TrainingHistoryRepository> logger)
	{
		_contextFactory = contextFactory;
		_logger = logger;
	}

	public async Task<Guid> CreateAsync(
		Guid trainedModelId,
		string name,
		string configJson,
		TrainingRunSnapshot run,
		CancellationToken cancellationToken = default
	)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = new TrainingHistoryEntry
		{
			Id = Guid.NewGuid(),
			TrainedModelId = trainedModelId,
			Name = name,
			ConfigJson = configJson,
			EpisodeMetricsJson = JsonSerializer.Serialize(run.Episodes),
			EpisodesCompleted = run.EpisodesCompleted,
			ModelEpisodesTrained = run.ModelEpisodesTrained,
			RecentAverageReward = run.RecentAverageReward,
			BestReward = run.BestReward,
			WorstReward = run.WorstReward,
			CreatedAt = DateTime.UtcNow,
		};
		context.TrainingHistoryEntries.Add(entity);
		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Saved training results '{Name}' as id {Id} for trained model {TrainedModelId} ({EpisodesCompleted} episodes)",
			name,
			entity.Id,
			trainedModelId,
			run.EpisodesCompleted
		);
		return entity.Id;
	}

	/// <summary>Lists saved results newest first -- for one model, or for every model when <paramref name="trainedModelId"/> is null.</summary>
	public async Task<IReadOnlyList<TrainingHistorySummary>> ListAsync(Guid? trainedModelId, CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var summaries = await context
			.TrainingHistoryEntries.Where(h => trainedModelId == null || h.TrainedModelId == trainedModelId)
			.OrderByDescending(h => h.CreatedAt)
			.Select(h => new TrainingHistorySummary(
				h.Id,
				h.TrainedModelId,
				h.TrainedModel.Name,
				h.Name,
				h.EpisodesCompleted,
				h.ModelEpisodesTrained,
				h.RecentAverageReward,
				h.BestReward,
				h.WorstReward,
				h.CreatedAt
			))
			.ToListAsync(cancellationToken);

		_logger.LogInformation(
			"Listed {EntryCount} training results (trained model filter: {TrainedModelId})",
			summaries.Count,
			trainedModelId?.ToString() ?? "none"
		);
		return summaries;
	}

	public async Task<TrainingHistoryData?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = await context
			.TrainingHistoryEntries.AsNoTracking()
			.Include(h => h.TrainedModel)
			.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
		if (entity is null)
		{
			_logger.LogWarning("Training results {Id} were not found", id);
			return null;
		}

		_logger.LogInformation("Loaded training results '{Name}' (id {Id})", entity.Name, id);
		var episodes = JsonSerializer.Deserialize<List<RlEpisodeMetric>>(entity.EpisodeMetricsJson) ?? [];
		return new TrainingHistoryData(
			new TrainingHistorySummary(
				entity.Id,
				entity.TrainedModelId,
				entity.TrainedModel.Name,
				entity.Name,
				entity.EpisodesCompleted,
				entity.ModelEpisodesTrained,
				entity.RecentAverageReward,
				entity.BestReward,
				entity.WorstReward,
				entity.CreatedAt
			),
			entity.ConfigJson,
			episodes
		);
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entity = await context.TrainingHistoryEntries.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
		if (entity is null)
		{
			_logger.LogWarning("Training results {Id} could not be deleted: not found", id);
			return false;
		}

		context.TrainingHistoryEntries.Remove(entity);
		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Deleted training results '{Name}' (id {Id})", entity.Name, id);
		return true;
	}
}
