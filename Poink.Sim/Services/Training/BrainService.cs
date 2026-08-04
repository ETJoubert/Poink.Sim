
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal sealed class BrainService
{
    private readonly IBrainRepository _brainRepository;
    private readonly ILogger<BrainService> _logger;

    public BrainService(IBrainRepository networkRepository, ILogger<BrainService> logger)
    {
        _brainRepository = networkRepository;
        _logger = logger;

        try
        {
            Brains = _brainRepository.ListBrains();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list brains at startup; continuing with none");
        }
    }


    public IReadOnlyList<BrainSummary> Brains { get; private set; } = [];


    public string? Status { get; private set; }

    public event Action? OnChanged;

    public bool CreateNetwork(string name, BrainRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "enter a name to save the network under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            _brainRepository.SaveBrain(name, recipe);
            Status = $"saved '{name}'";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"save failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to save network '{Name}'", name);
            return false;
        }
    }

    public bool UpdateNetwork(long id, string name, BrainRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "enter a name to save the network under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            _brainRepository.UpdateBrain(id, name, recipe);
            Status = $"updated '{name}'";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"update failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to update network id {Id}", id);
            return false;
        }
    }

    public bool DeleteNetwork(long id)
    {
        try
        {
            _brainRepository.DeleteBrain(id);
            Status = "network deleted";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"delete failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to delete network id {Id}", id);
            return false;
        }
    }

    public void Refresh()
    {
        try
        {
            Brains = _brainRepository.ListBrains();
        }
        catch (Exception ex)
        {
            Status = $"couldn't list brains: {ex.Message}";
            _logger.LogWarning(ex, "Failed to refresh brains list");
        }
        OnChanged?.Invoke();
    }
}
