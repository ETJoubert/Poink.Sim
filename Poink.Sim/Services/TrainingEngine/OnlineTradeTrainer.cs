using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Credits Trade Outcome learning with trade outcomes for one episode. An entry waits until its trade
/// closes, whatever closed it, and is then rewarded with that trade's realized value. A close is
/// rewarded with the trade it closed, and any other choice with zero. Only entries feed the reward
/// baseline, so a close or a hold is judged against what trades have actually returned.
/// </summary>
internal sealed class OnlineTradeTrainer
{
	private readonly PolicyGradientLearner _learner;
	private readonly MlpNetwork _network;
	private readonly ActionSpace _actionSpace;
	private readonly LearningOptionsModel _learning;
	private readonly double _progress;

	private DecisionTrace? _pendingEntry;

	public OnlineTradeTrainer(
		PolicyGradientLearner learner,
		MlpNetwork network,
		ActionSpace actionSpace,
		LearningOptionsModel learning,
		double progress
	)
	{
		_learner = learner;
		_network = network;
		_actionSpace = actionSpace;
		_learning = learning;
		_progress = progress;
	}

	public void SettleTrade(double realizedPnl)
	{
		if (_pendingEntry is not { } entry)
			return;

		_pendingEntry = null;
		_learner.LearnOnline(_network, _actionSpace, entry, realizedPnl, _learning, _progress, countsTowardBaseline: true);
	}

	public void RecordChoice(DecisionTrace trace, bool openedPosition, double? closedTradePnl)
	{
		if (openedPosition)
		{
			_pendingEntry = trace;
			return;
		}

		_learner.LearnOnline(_network, _actionSpace, trace, closedTradePnl ?? 0, _learning, _progress, countsTowardBaseline: false);
	}
}
