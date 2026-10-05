using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

internal interface IPriceReturnAnalysisService : IBackgroundAnalysisService
{
	PriceReturnDistributionResult? Result { get; }

	bool TryStart(PriceReturnRequest request);
}
