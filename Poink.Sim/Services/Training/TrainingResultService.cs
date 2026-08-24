using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal sealed class TrainingResultService : ITrainingResultService
{
	private readonly PoinkSimContext _context;
	private readonly ILogger<TrainingResultService> _logger;

	public TrainingResultService(PoinkSimContext context, ILogger<TrainingResultService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task<Guid> CreateAsync(
		string name,
		TrainingConfig config,
		TrainingSessionStats stats,
		TrainingResultsGraph graph,
		CancellationToken cancellationToken = default
	)
	{
		var entity = new Data.Entities.TrainingResult
		{
			Id = Guid.NewGuid(),
			Name = name,
			ConfigJson = JsonSerializer.Serialize(config),
			StatsJson = JsonSerializer.Serialize(stats),
			GraphJson = JsonSerializer.Serialize(graph),
			CreatedAt = DateTime.UtcNow,
		};

		_context.TrainingResults.Add(entity);
		await _context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Saved training result '{Name}' as id {Id}", name, entity.Id);

		return entity.Id;
	}

	public async Task<IReadOnlyList<TrainingResultSummary>> ListAsync(CancellationToken cancellationToken = default)
	{
		var entities = await _context
			.TrainingResults.OrderByDescending(r => r.CreatedAt)
			.ToListAsync(cancellationToken);

		return entities.Select(ToSummary).ToList();
	}

	public async Task<TrainingResultModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var entity = await _context.TrainingResults.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

		return entity is null ? null : ToModel(entity);
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var entity = await _context.TrainingResults.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
		if (entity is null)
			return false;

		_context.TrainingResults.Remove(entity);
		await _context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Deleted training result '{Name}' id {Id}", entity.Name, id);

		return true;
	}

	private static TrainingResultSummary ToSummary(Data.Entities.TrainingResult entity)
	{
		var config = JsonSerializer.Deserialize<TrainingConfig>(entity.ConfigJson)!;
		var stats = JsonSerializer.Deserialize<TrainingSessionStats>(entity.StatsJson)!;

		return new TrainingResultSummary(
			entity.Id,
			entity.Name,
			config.BrainName,
			config.StrategyName,
			entity.CreatedAt,
			stats.EpisodesCompleted,
			stats.TotalEpisodes,
			stats.RecentAverageReward,
			stats.BestReward
		);
	}

	private static TrainingResultModel ToModel(Data.Entities.TrainingResult entity) =>
		new(
			entity.Id,
			entity.Name,
			JsonSerializer.Deserialize<TrainingConfig>(entity.ConfigJson)!,
			JsonSerializer.Deserialize<TrainingSessionStats>(entity.StatsJson)!,
			JsonSerializer.Deserialize<TrainingResultsGraph>(entity.GraphJson)!,
			entity.CreatedAt
		);
}
