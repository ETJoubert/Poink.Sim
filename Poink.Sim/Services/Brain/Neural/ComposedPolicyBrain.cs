using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class ComposedPolicyBrain : IPolicyGradientBrain
{
	private const double CapitalFraction = 0.5;

	public static readonly IReadOnlyList<SimulationAction> AllDirections =
	[
		SimulationAction.Hold,
		SimulationAction.EnterLong,
		SimulationAction.EnterShort,
		SimulationAction.Close,
	];

	private readonly object _forwardLock = new();
	private readonly IObservationEncoder _encoder;
	private readonly IReadOnlyList<IActionOutputHead> _actionOutputHeads;
	private readonly IReadOnlyList<SimulationAction> _enabledDirections;
	private readonly DecisionDecodingKind _decisionDecoding;
	private readonly double _decisionTemperature;
	private readonly DirectionActionMasker _directionMasker;

	private ComposedPolicyBrain(
		string name,
		IObservationEncoder encoder,
		Module<Tensor, Tensor> network,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		DirectionActionMasker directionMasker
	)
	{
		Name = name;
		_encoder = encoder;
		Network = network;
		_actionOutputHeads = actionOutputHeads;
		_enabledDirections = enabledDirections;
		_decisionDecoding = decisionDecoding;
		_decisionTemperature = decisionTemperature;
		_directionMasker = directionMasker;
	}

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		params IObservationEncoder[] parts
	) => Create(name, architecture, actionOutputHeads, enabledDirections, DecisionDecodingKind.Softmax, 1.0, parts);

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		params IObservationEncoder[] parts
	) => Create(name, architecture, actionOutputHeads, enabledDirections, decisionDecoding, 1.0, parts);

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		CnnKernelSweepSource cnnKernelSweepSource,
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
		int extraOutputWidth = actionOutputHeads.Sum(h => h.OutputWidth);
		int outputWidth = enabledDirections.Count + extraOutputWidth;

		var network =
			architecture is CnnArchitecture cnnArchitecture
			&& cnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile
				? BuildCnnSweepNetwork(cnnArchitecture, parts, encoder.FeatureCount, outputWidth)
				: architecture.Build(encoder.FeatureCount, encoder.SequenceLength, outputWidth);

		var maskRules = BrainCatalog.CreateDirectionMaskRules(
			enabledMaskRules ?? BrainCatalog.AllDirectionMaskRuleKinds,
			parts
		);
		var directionMasker = new DirectionActionMasker(maskRules);
		return new ComposedPolicyBrain(
			name,
			encoder,
			network,
			actionOutputHeads,
			enabledDirections,
			decisionDecoding,
			decisionTemperature,
			directionMasker
		);
	}

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		CnnKernelSweepSource cnnKernelSweepSource,
		params IObservationEncoder[] parts
	) =>
		Create(
			name,
			architecture,
			actionOutputHeads,
			enabledDirections,
			decisionDecoding,
			decisionTemperature,
			cnnKernelSweepSource,
			null,
			parts
		);

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		IReadOnlyList<SimulationAction> enabledDirections,
		DecisionDecodingKind decisionDecoding,
		double decisionTemperature,
		params IObservationEncoder[] parts
	) =>
		Create(
			name,
			architecture,
			actionOutputHeads,
			enabledDirections,
			decisionDecoding,
			decisionTemperature,
			CnnKernelSweepSource.BarWindow,
			parts
		);

	private static Module<Tensor, Tensor> BuildCnnSweepNetwork(
		CnnArchitecture architecture,
		IReadOnlyList<IObservationEncoder> parts,
		int featureCount,
		int outputWidth
	)
	{
		int offset = 0;
		foreach (var part in parts)
		{
			if (part is ICnnSweepable sweepable)
				return architecture.BuildWithSweep(featureCount, offset, sweepable.SweepLength, outputWidth);
			offset += part.FeatureCount;
		}

		throw new InvalidOperationException(
			"CnnKernelSweepSource.VolumeProfile requires an ICnnSweepable encoder (e.g. VolumeProfileEncoder) in the brain's encoder list"
		);
	}

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		IReadOnlyList<IActionOutputHead> actionOutputHeads,
		params IObservationEncoder[] parts
	) => Create(name, architecture, actionOutputHeads, AllDirections, parts);

	public static IPolicyGradientBrain Create(
		string name,
		INetworkArchitecture architecture,
		params IObservationEncoder[] parts
	) => Create(name, architecture, [], AllDirections, parts);

	public string Name { get; }

	public Module<Tensor, Tensor> Network { get; }

	public int WarmupBars => _encoder.WarmupBars;

	private Device CurrentDevice => Network.parameters().First().device;

	public void Reset() { }

	public Tensor Encode(SimulationObservation observation) =>
		tensor(_encoder.Encode(observation)).unsqueeze(0).to(CurrentDevice);

	public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
	{
		var values = _encoder.Encode(observation);
		var descriptors = _encoder.Features;
		var features = new List<RlInputFeature>(values.Length);
		for (int i = 0; i < values.Length; i++)
			features.Add(new RlInputFeature(descriptors[i].Label, values[i], descriptors[i].Normalizer));
		return features;
	}

	public SimulationDecision Decide(SimulationObservation observation)
	{
		lock (_forwardLock)
		{
			using var scope = NewDisposeScope();
			using var noGradGuard = no_grad();
			return DecideWithLogProbability(observation).Decision;
		}
	}

	public (
		SimulationDecision Decision,
		Tensor ActionLogProbability,
		Tensor Entropy,
		IReadOnlyList<RlOutputFeature> Outputs
	) DecideWithLogProbability(SimulationObservation observation)
	{
		lock (_forwardLock)
		{
			var input = Encode(observation);
			var logits = Network.forward(input);

			var directionLogits = logits.narrow(1, 0, _enabledDirections.Count);
			var legalDirections = _directionMasker.Mask(_enabledDirections, observation);
			var maskedDirectionLogits = ApplyDirectionMask(directionLogits, legalDirections);

			var scaledDirectionLogits =
				_decisionDecoding == DecisionDecodingKind.HardMax
					? maskedDirectionLogits
					: maskedDirectionLogits / _decisionTemperature;
			var directionLogProbs = functional.log_softmax(scaledDirectionLogits, dim: 1);
			var directionProbabilities = exp(directionLogProbs);

			var entropy = -(directionProbabilities * directionLogProbs).sum();
			int actionIndex =
				_decisionDecoding == DecisionDecodingKind.HardMax
					? CategoricalSampling.ArgmaxIndex(directionProbabilities)
					: CategoricalSampling.SampleIndex(directionProbabilities);
			var actionLogProbability = directionLogProbs[0, actionIndex];
			var outputs = new List<RlOutputFeature>(
				DescribeOutputs(maskedDirectionLogits, directionProbabilities, actionIndex)
			);

			var headValues = new Dictionary<ActionOutputKind, double?>();
			int offset = _enabledDirections.Count;
			foreach (var head in _actionOutputHeads)
			{
				var slice = logits.narrow(1, offset, head.OutputWidth);
				var (value, logProbability, headOutputs) = head.Sample(slice);
				headValues[head.Kind] = value;
				actionLogProbability = actionLogProbability + logProbability;
				outputs.AddRange(headOutputs);
				offset += head.OutputWidth;
			}

			return (
				BuildDecision(_enabledDirections[actionIndex], headValues, observation.CurrentPrice),
				actionLogProbability,
				entropy,
				outputs
			);
		}
	}

	// A finite penalty (rather than -infinity) keeps log_softmax and the entropy's 0 * log(0) term finite.
	private const float IllegalDirectionPenalty = -1e9f;

	private static Tensor ApplyDirectionMask(Tensor logits, IReadOnlyList<bool> legalDirections)
	{
		var penalties = legalDirections.Select(legal => legal ? 0f : IllegalDirectionPenalty).ToArray();
		return logits + tensor(penalties).to(logits.device);
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
				Reason: "policy"
			),
			SimulationAction.EnterShort => new SimulationDecision(
				SimulationAction.EnterShort,
				capitalFraction,
				StopLossPrice: stopLossPercent is { } slShort ? currentPrice * (1 + slShort) : null,
				TakeProfitPrice: takeProfitPercent is { } tpShort ? currentPrice * (1 - tpShort) : null,
				Reason: "policy"
			),
			_ => new SimulationDecision(SimulationAction.Close, Reason: "policy"),
		};
	}
}
