// ── Convolutional network: Conv1d stack over a leading sequence slice + trailing scalars ──
//  Built by CnnArchitecture (see that file) from whatever IObservationEncoder
//  it's paired with, via sequenceLength/scalarCount rather than a
//  brain-specific constant -- same shape LstmPolicyNetwork takes for its
//  LSTM. Conv1d+Tanh stack (BuildConvStack, the same Array-to-Sequential
//  idiom MlpArchitecture uses for its Linear+Tanh stack) reduces the window
//  to one feature map, then the final timestep ("recent state") and the
//  mean over time ("overall regime") are pooled and concatenated with the
//  trailing scalar slice before a small MLP head produces one logit per
//  SimulationAction -- same last+mean pooling Poink.ML's VolTcnPatternBrain
//  uses for its own dilated-conv stack (that one uses ReLU/GELU since it's a
//  supervised-regression brain trained over many epochs, not a from-scratch
//  RL policy -- Tanh here instead, matching MlpArchitecture/
//  LstmPolicyNetwork's own heads, keeps every layer's output bounded to
//  [-1,1] so a freshly-initialized policy's logits stay modest and its
//  softmax starts reasonably spread instead of already saturated on
//  whichever action randomly got the largest pre-activation -- an unbounded
//  ReLU stack compounds layer over layer and pins the softmax hard on one
//  action from the very first tick, before training ever gets a chance to
//  explore). "Same" padding (kernelSize / 2) keeps the time length through
//  every conv for an odd kernel size; the pooling step reads the conv
//  stack's actual output length rather than assuming it, so an even kernel
//  size (off by one under this padding scheme) still narrows correctly
//  instead of throwing.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class CnnPolicyNetwork : Module<Tensor, Tensor>
{
    private readonly long _sequenceLength;
    private readonly Module<Tensor, Tensor> _convStack;
    private readonly Linear _head1;
    private readonly Linear _head2;

    public CnnPolicyNetwork(string name, long sequenceLength, long scalarCount, int[] channels, int kernelSize, long actionCount)
        : base(name)
    {
        _sequenceLength = sequenceLength;
        _convStack = BuildConvStack(channels, kernelSize);

        int lastChannels = channels[^1];
        _head1 = Linear(lastChannels * 2 + scalarCount, 32);
        _head2 = Linear(32, actionCount);

        // TorchSharp's equivalent of PyTorch's automatic submodule discovery
        // via self.foo = ... in __init__ -- required for this module's own
        // .parameters()/.to()/.save()/.load() to traverse _convStack's own
        // Conv1d layers plus _head1/_head2 (that's what makes
        // MoveToTrainingDevice/ExportWeights/etc. on IPolicyGradientBrain
        // work unmodified for this brain, and DescribeNetworkShape's
        // named_modules() walk find every Conv1d inside _convStack).
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
        var window = input.narrow(1, 0, _sequenceLength).unsqueeze(1); // (batch, 1, sequenceLength)
        var scalars = input.narrow(1, _sequenceLength, input.shape[1] - _sequenceLength); // (batch, scalarCount)

        var mapped = _convStack.forward(window); // (batch, channels, actualLength)

        var last = mapped.narrow(2, mapped.shape[2] - 1, 1).squeeze(-1); // (batch, channels) -- most recent timestep
        var mean = mapped.mean(new long[] { 2 }); // (batch, channels) -- overall window regime

        var combined = cat(new[] { last, mean, scalars }, dim: 1);
        var hidden = functional.tanh(_head1.forward(combined));
        return _head2.forward(hidden);
    }
}
