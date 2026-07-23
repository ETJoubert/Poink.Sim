// ── Recurrent network: LSTM over a leading sequence slice + trailing scalars ──
//  Moved out of the now-deleted LstmPolicyBrain.cs unchanged -- built by
//  LstmArchitecture (see that file) from whatever IObservationEncoder it's
//  paired with, via windowSize/scalarCount rather than a brain-specific
//  constant. LSTM over the sequence slice of a ComposedPolicyBrain's flat
//  feature vector; its final hidden state (top layer) is concatenated with
//  the trailing scalar slice and passed through a small MLP head to produce
//  one logit per SimulationAction. Hand-written (not Sequential) since
//  LSTM.forward returns a tuple, not a single Tensor Sequential can chain
//  through.
// ─────────────────────────────────────────────────────────────────────────
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class LstmPolicyNetwork : Module<Tensor, Tensor>
{
    private readonly long _windowSize;
    private readonly LSTM _lstm;
    private readonly Linear _head1;
    private readonly Linear _head2;

    public LstmPolicyNetwork(string name, long windowSize, long scalarCount, long hiddenSize, long numLayers, long actionCount)
        : base(name)
    {
        _windowSize = windowSize;
        _lstm = nn.LSTM(inputSize: 1, hiddenSize: hiddenSize, numLayers: numLayers, batchFirst: true);
        _head1 = nn.Linear(hiddenSize + scalarCount, 32);
        _head2 = nn.Linear(32, actionCount);

        // TorchSharp's equivalent of PyTorch's automatic submodule discovery
        // via self.foo = ... in __init__ -- required for this module's own
        // .parameters()/.to()/.save()/.load() to traverse _lstm/_head1/_head2
        // (that's what makes MoveToTrainingDevice/ExportWeights/etc. on
        // IPolicyGradientBrain work unmodified for this brain). Field
        // declaration order here also drives DescribeNetworkShape's
        // named_modules() traversal order -- don't reorder these three.
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        var window = input.narrow(1, 0, _windowSize).unsqueeze(-1); // (batch, windowSize, 1)
        var scalars = input.narrow(1, _windowSize, input.shape[1] - _windowSize); // (batch, scalarCount)

        var (_, hn, _) = _lstm.forward(window, null); // null h0_c0 -- fresh zero state per call, see LstmArchitecture's doc comment
        var lastHidden = hn[-1]; // (batch, hiddenSize) -- top layer's final hidden state

        var combined = cat(new[] { lastHidden, scalars }, dim: 1);
        var hidden = functional.tanh(_head1.forward(combined));
        return _head2.forward(hidden);
    }
}
