
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

internal sealed class NeuralNetworkRepository : INeuralNetworkRepository
{
    private readonly PoinkSimContext _context;
    private readonly ILogger<NeuralNetworkRepository> _logger;

    public NeuralNetworkRepository(PoinkSimContext context, ILogger<NeuralNetworkRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(string name, string structureJson, CancellationToken cancellationToken = default)
    {
        var entity = new NeuralNetwork
        {
            Id = Guid.NewGuid(),
            Name = name,
            StructureJson = structureJson,
            CreatedAt = DateTime.UtcNow,
        };

        _context.NeuralNetworks.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved neural network '{Name}' as id {Id}", name, entity.Id);
        return entity.Id;
    }

    public async Task<IReadOnlyList<NeuralNetworkModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.NeuralNetworks
            .OrderBy(n => n.Name)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Listed {NetworkCount} neural networks", entities.Count);
        return entities.Select(ToModel).ToList();
    }

    public async Task<NeuralNetworkModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralNetworks.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string structureJson, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralNetworks.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (entity is null) return false;

        entity.Name = name;
        entity.StructureJson = structureJson;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated neural network id {Id} ('{Name}')", id, name);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NeuralNetworks.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (entity is null) return false;

        _context.NeuralNetworks.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted neural network id {Id} ('{Name}')", id, entity.Name);
        return true;
    }

    private static NeuralNetworkModel ToModel(NeuralNetwork entity) =>
        new(entity.Id, entity.Name, entity.StructureJson, entity.CreatedAt, entity.UpdatedAt);
}
