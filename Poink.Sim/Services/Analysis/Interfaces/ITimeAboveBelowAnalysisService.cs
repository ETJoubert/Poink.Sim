using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

internal interface ITimeAboveBelowAnalysisService : IBackgroundAnalysisService
{
	TimeAboveBelowDistributionResult? Result { get; }

	bool TryStart(TimeAboveBelowRequest request);
}
