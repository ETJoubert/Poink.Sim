// ── Brain Builder CRUD ────────────────────────────────────────────────────
//  Thin persistence-only service over INetworkRepository -- Create/Read/
//  Update/Delete for a named BrainRecipe ("Network": encoders/architecture/
//  hidden sizes/actions, no weights). Deliberately independent of
//  RlTrainingService's live BrainRegistry/training concerns -- this is just
//  CRUD over saved configs, same cached-list/status-message/OnChanged shape
//  every other service here uses, so it drops into a Razor component the
//  same way RlTrainingService/SimulationEngine already do.
// ─────────────────────────────────────────────────────────────────────────
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

    // Cached, not re-queried on every render -- refreshed explicitly after a
    // create/update/delete (see Refresh) and once here at startup.
    public IReadOnlyList<BrainSummary> Brains { get; private set; } = [];

    // Feedback for the last Create/Update/Delete action -- read by whichever
    // Brain Builder UI ends up consuming this service.
    public string? Status { get; private set; }

    public event Action? OnChanged;

    // Always inserts a new row (see INetworkRepository's doc comment) --
    // callers needing "load, then continue editing" should track the
    // returned id and route further edits through UpdateNetwork instead of
    // calling this again under the same name.
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
