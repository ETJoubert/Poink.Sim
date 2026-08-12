
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

internal sealed class ModelService : IModelService
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


    public IReadOnlyList<TrainedModelSummary> Models { get; private set; } = [];


    public string? Status { get; private set; }

    public event Action? OnChanged;


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
