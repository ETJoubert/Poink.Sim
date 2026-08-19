using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Poink.Sim.Models;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;


public sealed record RlInputFeature(string Label, double Value, IFeatureDisplayNormalizer Normalizer);


public sealed record RlOutputFeature(string Label, double Probability, bool IsChosen, double Logit, string Group = "Direction");


public sealed record NetworkLayerShape(int Units, bool IsRecurrent);

internal interface IPolicyGradientBrain : ISimulationBrain
{

    Module<Tensor, Tensor> Network { get; }


    Tensor Encode(SimulationObservation observation);

    int WarmupBars => 0;


    (SimulationDecision Decision, Tensor ActionLogProbability, Tensor Entropy, IReadOnlyList<RlOutputFeature> Outputs) DecideWithLogProbability(
        SimulationObservation observation);


    IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation);


    byte[] ExportWeights()
    {
        using var stream = new MemoryStream();
        Network.save(stream);
        return stream.ToArray();
    }

    void ImportWeights(byte[] weights)
    {
        using var stream = new MemoryStream(weights);
        Network.load(stream);
    }


    long WeightCount() => Network.parameters().Sum(p => p.numel());

    void MoveToTrainingDevice() => Network.to(cuda.is_available() ? CUDA : CPU);

    void MoveToInferenceDevice() => Network.to(CPU);


    IReadOnlyList<NetworkLayerShape> DescribeNetworkShape()
    {
        var shapes = new List<NetworkLayerShape>();
        foreach (var (_, module) in Network.named_modules())
        {
            switch (module)
            {
                case Linear linear:
                    if (shapes.Count == 0)
                        shapes.Add(new NetworkLayerShape((int)linear.in_features, IsRecurrent: false));
                    shapes.Add(new NetworkLayerShape((int)linear.out_features, IsRecurrent: false));
                    break;
                case Conv1d conv1d:
                    if (shapes.Count == 0)
                        shapes.Add(new NetworkLayerShape((int)conv1d.in_channels, IsRecurrent: false));
                    shapes.Add(new NetworkLayerShape((int)conv1d.out_channels, IsRecurrent: false));
                    break;
                case LSTM lstm:
                    shapes.Add(new NetworkLayerShape(ReadLstmHiddenSize(lstm), IsRecurrent: true));
                    break;
            }
        }
        return shapes;
    }

    private static readonly FieldInfo? LstmHiddenSizeField =
        typeof(LSTM).GetField("_hidden_size", BindingFlags.NonPublic | BindingFlags.Instance);

    private static int ReadLstmHiddenSize(LSTM lstm) =>
        LstmHiddenSizeField?.GetValue(lstm) is long hiddenSize ? (int)hiddenSize : 0;
}
