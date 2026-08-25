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

	public LstmPolicyNetwork(
		string name,
		long windowSize,
		long scalarCount,
		long hiddenSize,
		long numLayers,
		long actionCount
	)
		: base(name)
	{
		_windowSize = windowSize;
		_lstm = nn.LSTM(inputSize: 1, hiddenSize: hiddenSize, numLayers: numLayers, batchFirst: true);
		_head1 = nn.Linear(hiddenSize + scalarCount, 32);
		_head2 = nn.Linear(32, actionCount);

		RegisterComponents();
	}

	// Runs the window through the LSTM from a fresh zero state each call (see
	// LstmArchitecture's doc comment), then combines the top layer's final hidden state with the scalar tail.
	public override Tensor forward(Tensor input)
	{
		var window = input.narrow(1, 0, _windowSize).unsqueeze(-1);
		var scalars = input.narrow(1, _windowSize, input.shape[1] - _windowSize);

		var (_, hn, _) = _lstm.forward(window, null);
		var lastHidden = hn[-1];

		var combined = cat(new[] { lastHidden, scalars }, dim: 1);
		var hidden = functional.tanh(_head1.forward(combined));
		return _head2.forward(hidden);
	}
}
