using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Saves, lists, loads and deletes trained models. A model is stored as the options it was trained
/// under plus its serialized weights; loading rebuilds the network and checks its shape still fits
/// those options, since weights only work with the input and output sizes they were trained with.
/// </summary>
internal sealed class TrainedModelService
{
	private readonly TrainedModelRepository _repository;
	private readonly MlpWeightsSerializer _serializer;
	private readonly ActionAssembler _actionAssembler;
	private readonly ILogger<TrainedModelService> _logger;

	public TrainedModelService(
		TrainedModelRepository repository,
		MlpWeightsSerializer serializer,
		ActionAssembler actionAssembler,
		ILogger<TrainedModelService> logger
	)
	{
		_repository = repository;
		_serializer = serializer;
		_actionAssembler = actionAssembler;
		_logger = logger;
	}

	public Task<IReadOnlyList<TrainedModelSummary>> ListAsync(CancellationToken cancellationToken = default) =>
		_repository.ListAsync(cancellationToken);

	public Task<Guid> SaveAsync(
		string name,
		TrainingOptionsModel options,
		MlpNetwork network,
		int episodesTrained,
		CancellationToken cancellationToken = default
	)
	{
		var trimmedName = name.Trim();
		if (trimmedName.Length == 0)
			throw new ArgumentException("A model needs a name to be saved.", nameof(name));

		if (ShapeMismatch(network, options) is { } mismatch)
		{
			_logger.LogWarning("Trained model '{Name}' was not saved: {Mismatch}", trimmedName, mismatch);
			throw new InvalidOperationException(
				$"The current options no longer match the network ({mismatch}). Revert the Features, Network or Output changes, or Reset and train a new network, before saving."
			);
		}

		return _repository.SaveAsync(
			trimmedName,
			JsonSerializer.Serialize(options),
			_serializer.Serialize(network),
			WeightCount(network),
			episodesTrained,
			cancellationToken
		);
	}

	public async Task<LoadedTrainedModel?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var data = await _repository.GetAsync(id, cancellationToken);
		if (data is null)
			return null;

		var options =
			JsonSerializer.Deserialize<TrainingOptionsModel>(data.ConfigJson)
			?? throw new InvalidOperationException($"Saved options for '{data.Summary.Name}' could not be read.");
		var network = _serializer.Deserialize(data.Weights);

		if (ShapeMismatch(network, options) is { } mismatch)
		{
			_logger.LogWarning("Trained model '{Name}' does not fit its saved options: {Mismatch}", data.Summary.Name, mismatch);
			throw new InvalidOperationException($"'{data.Summary.Name}' can't be loaded: its saved weights don't match its saved options ({mismatch}).");
		}

		return new LoadedTrainedModel(data.Summary, options, network, _actionAssembler.Assemble(options.Output));
	}

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, cancellationToken);

	private string? ShapeMismatch(MlpNetwork network, TrainingOptionsModel options)
	{
		var expectedInputSize = FeatureAssembler.ComputeInputSize(options.Features);
		var expectedOutputSize = _actionAssembler.Assemble(options.Output).TotalOutputWidth;
		var expectedHiddenSizes = NetworkAssembler.EffectiveHiddenSizes(options.Network);

		if (network.InputSize != expectedInputSize)
			return $"network takes {network.InputSize} inputs, options produce {expectedInputSize}";
		if (network.OutputSize != expectedOutputSize)
			return $"network gives {network.OutputSize} outputs, options expect {expectedOutputSize}";
		if (!network.HiddenSizes.SequenceEqual(expectedHiddenSizes))
			return $"network has hidden layers {string.Join("-", network.HiddenSizes)}, options say {string.Join("-", expectedHiddenSizes)}";
		return null;
	}

	private static long WeightCount(MlpNetwork network)
	{
		long count = 0;
		for (var layer = 0; layer < network.Weights.Count; layer++)
			count += network.Weights[layer].Length + network.Biases[layer].Length;
		return count;
	}
}
