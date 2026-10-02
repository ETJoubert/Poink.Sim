using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>Labels a signal dataset with what followed each point and ranks the signal states, then the best pairs of them.</summary>
internal interface ISignalAnalysisService
{
	Task<SignalAnalysisResult> AnalyzeAsync(SignalDatasetInfo dataset, SignalAnalysisOptions options, CancellationToken token = default);
}
