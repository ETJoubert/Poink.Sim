// ── Architecture: 1D convolution over a leading sequence slice + trailing scalars ──
//  Sibling to LstmArchitecture -- same leading-sequence-slice/trailing-scalars
//  contract (see that file and IObservationEncoder's doc comment), a stack of
//  Conv1d layers instead of an LSTM. channels/kernelSize are this
//  architecture's knobs; sequenceLength (the window width) and scalarCount
//  come from whichever IObservationEncoder it's paired with at Build time,
//  same as LstmArchitecture.
// ─────────────────────────────────────────────────────────────────────────
using System;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class CnnArchitecture : INetworkArchitecture
{
    private readonly int _kernelSize;
    private readonly int[] _channels;

    public CnnArchitecture(int kernelSize, params int[] channels)
    {
        _kernelSize = kernelSize;
        _channels = channels;
    }

    public string Name => $"CNN-{string.Join('-', _channels)}k{_kernelSize}";

    public Module<Tensor, Tensor> Build(int featureCount, int sequenceLength, int outputWidth)
    {
        if (sequenceLength <= 0)
            throw new InvalidOperationException(
                "CnnArchitecture requires an encoder with a sequence-bearing leading slice -- " +
                "pair it with an encoder like BarWindowEncoder listed first");

        return new CnnPolicyNetwork(
            "cnn_policy", sequenceLength: sequenceLength, scalarCount: featureCount - sequenceLength,
            channels: _channels, kernelSize: _kernelSize, actionCount: outputWidth);
    }

    // CNN-only alternate entry point (see ICnnSweepable): sweeps a slice
    // that isn't necessarily the composite's leading one -- sweepOffset/
    // sweepLength come from ComposedPolicyBrain.Create locating whichever
    // part a BrainRecipe.CnnKernelSweepSource of VolumeProfile resolves to,
    // rather than always assuming offset 0 like Build above.
    public Module<Tensor, Tensor> BuildWithSweep(int featureCount, int sweepOffset, int sweepLength, int outputWidth)
    {
        if (sweepLength <= 0)
            throw new InvalidOperationException(
                "CnnArchitecture requires a sweep source with a positive SweepLength -- " +
                "pair it with an encoder like VolumeProfileEncoder that implements ICnnSweepable");

        return new CnnPolicyNetwork(
            "cnn_policy", sequenceLength: sweepLength, scalarCount: featureCount - sweepLength,
            channels: _channels, kernelSize: _kernelSize, actionCount: outputWidth, sequenceOffset: sweepOffset);
    }
}
