// ── Trained-model CRUD ────────────────────────────────────────────────────
//  Thin persistence-only service over IModelRepository -- Create/Read/Delete
//  for a trained model snapshot (exported weights + the strategy/parameters/
//  metrics of the run that produced them). Deliberately independent of
//  RlTrainingService's live BrainRegistry/training concerns -- this is just
//  CRUD over saved snapshots, same cached-list/status-message/OnChanged
//  shape brainservice and every other service here uses. No Update: like
//  IModelRepository/BrainRepository before it, every save is its own
//  snapshot rather than an in-place edit (see IModelRepository's doc
//  comment on why).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal sealed class ModelService
{
    private readonly IModelRepository _modelRepository;
    private readonly ILogger<ModelService> _logger;

    public ModelService(IModelRepository modelRepository, ILogger<ModelService> logger)
    {
        _modelRepository = modelRepository;
        _logger = logger;

        try
        {
            Models = _modelRepository.ListModels();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list models at startup; continuing with none");
        }
    }

    // Cached, not re-queried on every render -- refreshed explicitly after a
    // save/delete (see Refresh) and once here at startup.
    public IReadOnlyList<TrainedModelSummary> Models { get; private set; } = [];

    // Feedback for the last Save/Delete action -- read by whichever page ends
    // up consuming this service.
    public string? Status { get; private set; }

    public event Action? OnChanged;

    // Always inserts a new row (see IModelRepository's doc comment) -- every
    // save is its own history entry, never an upsert over a prior one.
    public bool SaveModel(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        ModelTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "enter a name to save the model under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            long id = _modelRepository.SaveModel(
                name, brainName, strategyName, rewardPolicyName, dataset, parameters, recipe, trainedAt, weightCount, avgReward, weights);
            Status = $"saved '{name}' as id {id}";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"save failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to save model '{Name}'", name);
            return false;
        }
    }

    // Null if the model no longer exists -- a listing screen never pulls
    // this itself (see TrainedModelSummary's doc comment), so callers fetch
    // it on demand, e.g. right before ImportWeights-ing it into a live brain.
    public byte[]? LoadWeights(long id) => _modelRepository.LoadWeights(id);

    public bool DeleteModel(long id)
    {
        try
        {
            _modelRepository.DeleteModel(id);
            Status = "model deleted";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"delete failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to delete model id {Id}", id);
            return false;
        }
    }

    public void Refresh()
    {
        try
        {
            Models = _modelRepository.ListModels();
        }
        catch (Exception ex)
        {
            Status = $"couldn't list models: {ex.Message}";
            _logger.LogWarning(ex, "Failed to refresh models list");
        }
        OnChanged?.Invoke();
    }
}
