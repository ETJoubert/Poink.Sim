// ── Architecture: LSTM over a leading sequence slice + trailing scalars ──
//  Generalizes the hand-written LstmPolicyNetwork wiring the now-deleted
//  LstmPolicyBrain used to hardcode (windowSize=20, hiddenSize=64,
//  numLayers=2) -- hiddenSize/numLayers are this architecture's knobs;
//  windowSize (the sequence slice width) and scalarCount come from whichever
//  IObservationEncoder it's paired with at Build time.
// ─────────────────────────────────────────────────────────────────────────
using System;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class LstmArchitecture : INetworkArchitecture
{
    private readonly int _hiddenSize;
    private readonly int _numLayers;

    public LstmArchitecture(int hiddenSize, int numLayers)
    {
        _hiddenSize = hiddenSize;
        _numLayers = numLayers;
    }

    public string Name => $"LSTM-{_hiddenSize}x{_numLayers}";

    public Module<Tensor, Tensor> Build(int featureCount, int sequenceLength, int outputWidth)
    {
        if (sequenceLength <= 0)
            throw new InvalidOperationException(
                "LstmArchitecture requires an encoder with a sequence-bearing leading slice -- " +
                "pair it with an encoder like BarWindowEncoder listed first");

        return new LstmPolicyNetwork(
            "lstm_policy", windowSize: sequenceLength, scalarCount: featureCount - sequenceLength,
            hiddenSize: _hiddenSize, numLayers: _numLayers, actionCount: outputWidth);
    }
}
