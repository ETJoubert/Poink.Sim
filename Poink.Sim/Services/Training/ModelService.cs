using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;

public sealed record NeuralWeightsSummary(
	Guid Id,
	string Name,
	string BrainName,
	string StrategyName,
	string RewardPolicyName,
	string Dataset,
	ModelTrainingParameters Parameters,
	BrainRecipe? Recipe,
	DateTime? TrainedAt,
	long WeightCount,
	double AvgReward,
	DateTime SavedAt
);

internal sealed class ModelService : IModelService
{
	private readonly INeuralWeightsService _weightsService;
	private readonly INeuralNetworkService _networkService;
	private readonly ILogger<ModelService> _logger;

	public ModelService(
		INeuralWeightsService weightsService,
		INeuralNetworkService networkService,
		ILogger<ModelService> logger
	)
	{
		_weightsService = weightsService;
		_networkService = networkService;
		_logger = logger;

		try
		{
			Models = LoadModels();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to list models at startup; continuing with none");
		}
	}

	public IReadOnlyList<NeuralWeightsSummary> Models { get; private set; } = [];

	public string? Status { get; private set; }

	public event Action? OnChanged;

	// INeuralWeightsService/INeuralNetworkService are themselves singletons (backed by
	// IDbContextFactory); Task.Run just bridges their async API onto this synchronous call.
	private static T RunSync<T>(Func<Task<T>> action) => Task.Run(action).GetAwaiter().GetResult();

	private IReadOnlyList<NeuralWeightsSummary> LoadModels() =>
		RunSync(async () =>
		{
			var weights = await _weightsService.ListAllAsync();
			var networkNamesById = (await _networkService.ListAsync()).ToDictionary(n => n.Id, n => n.Name);
			return (IReadOnlyList<NeuralWeightsSummary>)
				weights.Select(w => ToSummary(w, networkNamesById.GetValueOrDefault(w.NeuralNetworkId, "?"))).ToList();
		});

	public bool SaveModel(
		string name,
		string brainName,
		string strategyName,
		string rewardPolicyName,
		string dataset,
		ModelTrainingParameters parameters,
		BrainRecipe? recipe,
		DateTime? trainedAt,
		long weightCount,
		double avgReward,
		byte[] weights
	)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			Status = "enter a name to save the model under";
			OnChanged?.Invoke();
			return false;
		}

		try
		{
			var id = RunSync(async () =>
			{
				var network =
					(await _networkService.ListAsync()).FirstOrDefault(n => n.Name == brainName)
					?? throw new InvalidOperationException($"no saved network named '{brainName}'");

				string parametersJson = JsonSerializer.Serialize(parameters);
				string? structureSnapshotJson = recipe is null ? null : _networkService.EncodeRecipe(recipe);

				return await _weightsService.CreateAsync(
					network.Id,
					name,
					strategyName,
					rewardPolicyName,
					dataset,
					parametersJson,
					structureSnapshotJson,
					weights,
					weightCount,
					avgReward,
					trainedAt
				);
			});

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

	public byte[]? LoadWeights(Guid id) => RunSync(async () => (await _weightsService.GetAsync(id))?.Data);

	public bool DeleteModel(Guid id)
	{
		try
		{
			RunSync(() => _weightsService.DeleteAsync(id));
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
			Models = LoadModels();
		}
		catch (Exception ex)
		{
			Status = $"couldn't list models: {ex.Message}";
			_logger.LogWarning(ex, "Failed to refresh models list");
		}
		OnChanged?.Invoke();
	}

	private NeuralWeightsSummary ToSummary(NeuralWeightsModel model, string brainName) =>
		new(
			model.Id,
			model.Name,
			brainName,
			model.StrategyName,
			model.RewardPolicyName,
			model.Dataset,
			JsonSerializer.Deserialize<ModelTrainingParameters>(model.ParametersJson) ?? new ModelTrainingParameters(),
			model.StructureSnapshotJson is null ? null : _networkService.DecodeRecipe(model.StructureSnapshotJson),
			model.TrainedAt,
			model.WeightCount,
			model.AvgReward,
			model.CreatedAt
		);
}
