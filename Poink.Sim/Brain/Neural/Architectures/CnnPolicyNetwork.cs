
using System.Collections.Generic;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class CnnPolicyNetwork : Module<Tensor, Tensor>
{
    private readonly long _sequenceOffset;
    private readonly long _sequenceLength;
    private readonly Module<Tensor, Tensor> _convStack;
    private readonly Linear _head1;
    private readonly Linear _head2;

    public CnnPolicyNetwork(
        string name, long sequenceLength, long scalarCount, int[] channels, int kernelSize, long actionCount, long sequenceOffset = 0)
        : base(name)
    {
        _sequenceOffset = sequenceOffset;
        _sequenceLength = sequenceLength;
        _convStack = BuildConvStack(channels, kernelSize);

        int lastChannels = channels[^1];
        _head1 = Linear(lastChannels * 2 + scalarCount, 32);
        _head2 = Linear(32, actionCount);


        RegisterComponents();
    }

    private static Module<Tensor, Tensor> BuildConvStack(int[] channels, int kernelSize)
    {
        var layers = new List<(string, Module<Tensor, Tensor>)>();
        int inChannels = 1;
        for (int i = 0; i < channels.Length; i++)
        {
            layers.Add(($"conv{i + 1}", Conv1d(inChannels, channels[i], kernelSize, padding: kernelSize / 2)));
            layers.Add(($"fold{i + 1}", Tanh()));
            inChannels = channels[i];
        }
        return Sequential(layers.ToArray());
    }

    public override Tensor forward(Tensor input)
    {
        var window = input.narrow(1, _sequenceOffset, _sequenceLength).unsqueeze(1); // (batch, 1, sequenceLength)

        long afterStart = _sequenceOffset + _sequenceLength;
        long afterLength = input.shape[1] - afterStart;
        var scalars = _sequenceOffset == 0
            ? input.narrow(1, afterStart, afterLength)
            : cat(new[] { input.narrow(1, 0, _sequenceOffset), input.narrow(1, afterStart, afterLength) }, dim: 1);

        var mapped = _convStack.forward(window); // (batch, channels, actualLength)

        var last = mapped.narrow(2, mapped.shape[2] - 1, 1).squeeze(-1); // (batch, channels) -- most recent timestep
        var mean = mapped.mean(new long[] { 2 }); // (batch, channels) -- overall window regime

        var combined = cat(new[] { last, mean, scalars }, dim: 1);
        var hidden = functional.tanh(_head1.forward(combined));
        return _head2.forward(hidden);
    }
}
