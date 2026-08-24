using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class NeatPolicyBrain : INeatPolicyBrain
{
	private const double CapitalFraction = 0.5;
	private const float IllegalDirectionPenalty = -1e9f;

	private readonly object _decideLock = new();
	private readonly IObservationEncoder _encoder;
	private readonly IReadOnlyList<IActionOutputHead> _actionOutputHeads;
	private readonly IReadOnlyList<SimulationAction> _enabledDirections;
	private readonly DecisionDecodingKind _decisionDecoding;
	private readonly double _decisionTemperature;
	private readonly DirectionActionMasker _directionMasker;

	private NeatPolicyBrain(
		string name,
		IObservationEncoder encoder,
		NeatPopulation population,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		DirectionActionMasker directionMasker
	)
	{
		Name = name;
		_encoder = encoder;
		Population = population;
		_actionOutputHeads = actionOutputHeads;
		_enabledDirections = enabledDirections;
		_decisionDecoding = decisionDecoding;
		_decisionTemperature = decisionTemperature;
		_directionMasker = directionMasker;
	}

	public static INeatPolicyBrain Create(
		string name,
		NeatHyperparameters hyperparameters,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		IReadOnlyList<DirectionMaskRuleKind>? enabledMaskRules,
		params IObservationEncoder[] parts
	)
	{
		if (enabledDirections.Count == 0)
			throw new ArgumentException(
				"a brain needs at least one enabled direction (Hold/EnterLong/EnterShort/Close)",
				nameof(enabledDirections)
			);

		var encoder = CompositeEncoder.Create(parts);
		int outputWidth = enabledDirections.Count + actionOutputHeads.Sum(h => h.OutputWidth);
		var population = new NeatPopulation(encoder.FeatureCount, outputWidth, hyperparameters);

		var maskRules = BrainCatalog.CreateDirectionMaskRules(
			enabledMaskRules ?? BrainCatalog.AllDirectionMaskRuleKinds,
			parts
		);
		var directionMasker = new DirectionActionMasker(maskRules);
		return new NeatPolicyBrain(
			name,
			encoder,
			population,
			actionOutputHeads,
			enabledDirections,
			decisionDecoding,
			decisionTemperature,
			directionMasker
		);
	}

	public string Name { get; }

	public NeatPopulation Population { get; }

	public int WarmupBars => _encoder.WarmupBars;

	public void Reset() { } // population state persists deliberately across runs/episodes

	public SimulationDecision Decide(SimulationObservation observation) => DecideWithOutputs(observation).Decision;

	// Always the genome currently under evaluation (not Population.BestGenome) -- that one only
	// changes once per generation, which would leave the live view frozen for every episode in
	// between; CurrentGenome updates every episode, same cadence as everything else in TrainingPreview.
	public NeatTopologySnapshot DescribeTopology() =>
		NeatTopologyDescriber.Describe(Population.CurrentGenome, Population.Generation);

	public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
	{
		var values = _encoder.Encode(observation);
		var descriptors = _encoder.Features;
		var features = new List<RlInputFeature>(values.Length);
		for (int i = 0; i < values.Length; i++)
			features.Add(new RlInputFeature(descriptors[i].Label, values[i], descriptors[i].Normalizer));
		return features;
	}

	public (SimulationDecision Decision, IReadOnlyList<RlOutputFeature> Outputs) DecideWithOutputs(
		SimulationObservation observation
	)
	{
		lock (_decideLock)
		{
			using var scope = NewDisposeScope();
			using var noGradGuard = no_grad();

			var inputs = _encoder.Encode(observation);
			var rawOutputs = NeatNetwork.Forward(Population.CurrentGenome, inputs);
			var logits = tensor(rawOutputs).unsqueeze(0);

			var directionLogits = logits.narrow(1, 0, _enabledDirections.Count);
			var legalDirections = _directionMasker.Mask(_enabledDirections, observation);
			var maskedDirectionLogits = ApplyDirectionMask(directionLogits, legalDirections);

			var scaledDirectionLogits =
				_decisionDecoding == DecisionDecodingKind.HardMax
					? maskedDirectionLogits
					: maskedDirectionLogits / _decisionTemperature;
			var directionLogProbs = functional.log_softmax(scaledDirectionLogits, dim: 1);
			var directionProbabilities = exp(directionLogProbs);

			int actionIndex =
				_decisionDecoding == DecisionDecodingKind.HardMax
					? CategoricalSampling.ArgmaxIndex(directionProbabilities)
					: CategoricalSampling.SampleIndex(directionProbabilities);
			var outputs = new List<RlOutputFeature>(
				DescribeOutputs(maskedDirectionLogits, directionProbabilities, actionIndex)
			);

			var headValues = new Dictionary<ActionOutputKind, double?>();
			int offset = _enabledDirections.Count;
			foreach (var head in _actionOutputHeads)
			{
				var slice = logits.narrow(1, offset, head.OutputWidth);
				var (value, _, headOutputs) = head.Sample(slice);
				headValues[head.Kind] = value;
				outputs.AddRange(headOutputs);
				offset += head.OutputWidth;
			}

			return (BuildDecision(_enabledDirections[actionIndex], headValues, observation.CurrentPrice), outputs);
		}
	}

	private static Tensor ApplyDirectionMask(Tensor logits, IReadOnlyList<bool> legalDirections)
	{
		var penalties = legalDirections.Select(legal => legal ? 0f : IllegalDirectionPenalty).ToArray();
		return logits + tensor(penalties);
	}

	private IReadOnlyList<RlOutputFeature> DescribeOutputs(
		Tensor actionLogits,
		Tensor actionProbabilities,
		int chosenIndex
	)
	{
		var outputs = new List<RlOutputFeature>(_enabledDirections.Count);
		for (int i = 0; i < _enabledDirections.Count; i++)
			outputs.Add(
				new RlOutputFeature(
					_enabledDirections[i].ToString(),
					actionProbabilities[0, i].item<float>(),
					i == chosenIndex,
					actionLogits[0, i].item<float>(),
					Group: "Direction"
				)
			);
		return outputs;
	}

	private static SimulationDecision BuildDecision(
		SimulationAction action,
		IReadOnlyDictionary<ActionOutputKind, double?> headValues,
		double currentPrice
	)
	{
		double capitalFraction =
			headValues.TryGetValue(ActionOutputKind.Volume, out var volume) && volume is { } v ? v : CapitalFraction;
		double? stopLossPercent = headValues.TryGetValue(ActionOutputKind.StopLoss, out var sl) ? sl : null;
		double? takeProfitPercent = headValues.TryGetValue(ActionOutputKind.TakeProfit, out var tp) ? tp : null;

		return action switch
		{
			SimulationAction.Hold => new SimulationDecision(SimulationAction.Hold),
			SimulationAction.EnterLong => new SimulationDecision(
				SimulationAction.EnterLong,
				capitalFraction,
				StopLossPrice: stopLossPercent is { } slLong ? currentPrice * (1 - slLong) : null,
				TakeProfitPrice: takeProfitPercent is { } tpLong ? currentPrice * (1 + tpLong) : null,
				Reason: "neat"
			),
			SimulationAction.EnterShort => new SimulationDecision(
				SimulationAction.EnterShort,
				capitalFraction,
				StopLossPrice: stopLossPercent is { } slShort ? currentPrice * (1 + slShort) : null,
				TakeProfitPrice: takeProfitPercent is { } tpShort ? currentPrice * (1 - tpShort) : null,
				Reason: "neat"
			),
			_ => new SimulationDecision(SimulationAction.Close, Reason: "neat"),
		};
	}
}
