using System.Collections.Generic;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>Lists and deletes stored signal datasets.</summary>
internal interface ISignalDatasetService
{
	Task<IReadOnlyList<SignalDatasetInfo>> ListAsync();

	Task DeleteAsync(long datasetId);
}
