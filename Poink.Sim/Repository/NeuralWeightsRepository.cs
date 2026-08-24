
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
    private readonly PoinkSimContext _context;
    private readonly ILogger<NeuralWeightsRepository> _logger;

    public NeuralWeightsRepository(PoinkSimContext context, ILogger<NeuralWeightsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(Guid neuralNetworkId, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default)
    {
        var entity = new NeuralWeights
        {
            Id = Guid.NewGuid(),
            NeuralNetworkId = neuralNetworkId,
            Name = name,
            Data = data,
            WeightCount = weightCount,
            CreatedAt = DateTime.UtcNow,
        };

        _context.NeuralWeights.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved neural weights '{Name}' as id {Id} for network {NeuralNetworkId}", name, entity.Id, neuralNetworkId);
        return entity.Id;
    }

    public async Task<IReadOnlyList<NeuralWeightsModel>> ListByNetworkAsync(Guid neuralNetworkId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.NeuralWeights
            .Where(w => w.NeuralNetworkId == neuralNetworkId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Listed {WeightsCount} neural weights for network {NeuralNetworkId}", entities.Count, neuralNetworkId);
        return entities.Select(ToModel).ToList();
    }

    public async Task<NeuralWeightsModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, string name, byte[] data, long weightCount, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null) return false;

        entity.Name = name;
        entity.Data = data;
        entity.WeightCount = weightCount;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated neural weights id {Id} ('{Name}')", id, name);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralWeights.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (entity is null) return false;

        _context.NeuralWeights.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted neural weights id {Id} ('{Name}')", id, entity.Name);
        return true;
    }

    private static NeuralWeightsModel ToModel(NeuralWeights entity) =>
        new(entity.Id, entity.NeuralNetworkId, entity.Name, entity.Data, entity.WeightCount, entity.CreatedAt);
}
