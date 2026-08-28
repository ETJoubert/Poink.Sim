using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class DiscretizedLevelsActionHead : IActionOutputHead
{
	private static readonly IReadOnlyDictionary<ActionOutputKind, (string Label, double? Value)[]> LevelsByKind =
		new Dictionary<ActionOutputKind, (string, double?)[]>
		{
			[ActionOutputKind.Volume] = [("25%", 0.25), ("50%", 0.5), ("75%", 0.75), ("100%", 1.0)],
			[ActionOutputKind.StopLoss] = [("None", null), ("Tight", 0.0015), ("Medium", 0.003), ("Wide", 0.006)],
			[ActionOutputKind.TakeProfit] = [("None", null), ("Tight", 0.003), ("Medium", 0.006), ("Wide", 0.012)],
		};

	private readonly (string Label, double? Value)[] _levels;
	private readonly DecisionDecodingKind _decoding;
	private readonly double _temperature;

	public DiscretizedLevelsActionHead(
		ActionOutputKind kind,
		DecisionDecodingKind decoding = DecisionDecodingKind.Softmax,
		double temperature = 1.0
	)
	{
		Kind = kind;
		_levels = LevelsByKind[kind];
		_decoding = decoding;
		_temperature = temperature;
	}

	public ActionOutputKind Kind { get; }

	public int OutputWidth => _levels.Length;

	public static IReadOnlyList<string> GetLevelLabels(ActionOutputKind kind) =>
		LevelsByKind[kind].Select(l => l.Label).ToList();

	public (double? Value, Tensor LogProbability, int LevelIndex, IReadOnlyList<RlOutputFeature> Outputs) Sample(
		Tensor rawSlice
	)
	{
		var scaledSlice = _decoding == DecisionDecodingKind.HardMax ? rawSlice : rawSlice / _temperature;
		var logProbs = functional.log_softmax(scaledSlice, dim: 1);
		var probabilities = exp(logProbs);
		int levelIndex =
			_decoding == DecisionDecodingKind.HardMax
				? CategoricalSampling.ArgmaxIndex(probabilities)
				: CategoricalSampling.SampleIndex(probabilities);

		var outputs = new List<RlOutputFeature>(_levels.Length);
		for (int i = 0; i < _levels.Length; i++)
			outputs.Add(
				new RlOutputFeature(
					_levels[i].Label,
					probabilities[0, i].item<float>(),
					i == levelIndex,
					rawSlice[0, i].item<float>(),
					Group: Kind.ToString()
				)
			);

		return (_levels[levelIndex].Value, logProbs[0, levelIndex], levelIndex, outputs);
	}

	public Tensor LogProbabilityOf(Tensor rawSlice, int levelIndex)
	{
		var scaledSlice = _decoding == DecisionDecodingKind.HardMax ? rawSlice : rawSlice / _temperature;
		var logProbs = functional.log_softmax(scaledSlice, dim: 1);
		return logProbs[0, levelIndex];
	}
}
