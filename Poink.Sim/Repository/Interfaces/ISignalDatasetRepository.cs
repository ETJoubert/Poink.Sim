using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Repository;

/// <summary>Stores signal datasets: one header per dataset and its per-second signal points.</summary>
internal interface ISignalDatasetRepository
{
	/// <summary>Starts a dataset in the Building state and returns its id.</summary>
	long CreateDataset(SignalDatasetBuildRequest request);

	void AppendPoints(long datasetId, IReadOnlyList<SignalPoint> points);

	/// <summary>Records how the build ended, with the signal rows its points' mask bits stand for.</summary>
	void FinishDataset(long datasetId, SignalDatasetStatus status, IReadOnlyList<SignalCatalogEntry> catalog, long pointCount);

	IReadOnlyList<SignalDatasetInfo> ListDatasets();

	void DeleteDataset(long datasetId);

	/// <summary>Streams a dataset's points oldest first.</summary>
	IEnumerable<SignalPoint> ReadPoints(long datasetId);
}
