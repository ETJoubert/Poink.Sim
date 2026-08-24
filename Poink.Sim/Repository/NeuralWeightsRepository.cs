
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Data.Entities;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

internal sealed class NeuralWeightsRepository : INeuralWeightsRepository
{
    private readonly IDbContextFactory<PoinkSimContext> _contextFactory;
    private readonly ILogger<NeuralWeightsRepository> _logger;

    public NeuralWeightsRepository(IDbContextFactory<PoinkSimContext> contextFactory, ILogger<NeuralWeightsRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(
        Guid neuralNetworkId, string name, string strategyName, string rewardPolicyName, string dataset,
        string parametersJson, string? structureSnapshotJson, byte[] data, long weightCount, double avgReward,
        DateTime? trainedAt, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new NeuralWeights
        {
            Id = Guid.NewGuid(),
            NeuralNetworkId = neuralNetworkId,
            Name = name,
            StrategyName = strategyName,
            RewardPolicyName = rewardPolicyName,
            Dataset = dataset,
            ParametersJson = parametersJson,
            StructureSnapshotJson = structureSnapshotJson,
            Data = data,
            WeightCount = weightCount,
            AvgReward = avgReward,
            TrainedAt = trainedAt,
            CreatedAt = DateTime.UtcNow,
        };

        context.NeuralWeights.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved neural weights '{Name}' as id {Id} for network {NeuralNetworkId}", name, entity.Id, neuralNetworkId);
        return entity.Id;
    }

    public async Task<IReadOnlyList<NeuralWeightsModel>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await context.NeuralWeights
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Listed {WeightsCount} neural weights", entities.Count);
        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<NeuralWeightsModel>> ListByNetworkAsync(Guid neuralNetworkId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await context.NeuralWeights
            .Where(w => w.NeuralNetworkId == neuralNetworkId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Listed {WeightsCount} neural weights for network {NeuralNetworkId}", entities.Count, neuralNetworkId);
        return entities.Select(ToModel).ToList();
    }

    public async Task<NeuralWeightsModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<bool> UpdateAsync(
        Guid id, string name, string strategyName, string rewardPolicyName, string dataset,
        string parametersJson, string? structureSnapshotJson, byte[] data, long weightCount, double avgReward,
        DateTime? trainedAt, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null) return false;

        entity.Name = name;
        entity.StrategyName = strategyName;
        entity.RewardPolicyName = rewardPolicyName;
        entity.Dataset = dataset;
        entity.ParametersJson = parametersJson;
        entity.StructureSnapshotJson = structureSnapshotJson;
        entity.Data = data;
        entity.WeightCount = weightCount;
        entity.AvgReward = avgReward;
        entity.TrainedAt = trainedAt;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated neural weights id {Id} ('{Name}')", id, name);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null) return false;

        context.NeuralWeights.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted neural weights id {Id} ('{Name}')", id, entity.Name);
        return true;
    }

    private static NeuralWeightsModel ToModel(NeuralWeights entity) =>
        new(
            entity.Id, entity.NeuralNetworkId, entity.Name, entity.StrategyName, entity.RewardPolicyName, entity.Dataset,
            entity.ParametersJson, entity.StructureSnapshotJson, entity.Data, entity.WeightCount, entity.AvgReward,
            entity.TrainedAt, entity.CreatedAt);
}
