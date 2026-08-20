
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

internal interface ITrainingResultService
{
    Task<Guid> CreateAsync(
        string name, TrainingConfig config, TrainingSessionStats stats, TrainingResultsGraph graph,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrainingResultSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<TrainingResultModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
