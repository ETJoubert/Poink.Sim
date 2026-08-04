
using System.Collections.Generic;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class MlpArchitecture : INetworkArchitecture
{
    private readonly int[] _hiddenSizes;

    public MlpArchitecture(params int[] hiddenSizes) => _hiddenSizes = hiddenSizes;

    public string Name => $"MLP-{string.Join('-', _hiddenSizes)}";

    // sequenceLength is intentionally unused -- a flat MLP treats the whole
    // vector as one input, sequence or not.
    public Module<Tensor, Tensor> Build(int featureCount, int sequenceLength, int outputWidth)
    {
        var layers = new List<(string, Module<Tensor, Tensor>)>();
        int inFeatures = featureCount;
        for (int i = 0; i < _hiddenSizes.Length; i++)
        {
            layers.Add(($"hidden{i + 1}", Linear(inFeatures, _hiddenSizes[i])));
            layers.Add(($"fold{i + 1}", Tanh()));
            inFeatures = _hiddenSizes[i];
        }
        layers.Add(("heads", Linear(inFeatures, outputWidth)));

        return Sequential(layers.ToArray());
    }
}
