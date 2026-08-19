
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal interface INetworkArchitecture
{

    string Name { get; }


    Module<Tensor, Tensor> Build(int featureCount, int sequenceLength, int outputWidth);
}
