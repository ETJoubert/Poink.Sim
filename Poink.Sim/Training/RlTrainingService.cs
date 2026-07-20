// ── Headless RL training orchestrator ────────────────────────────────────
//  Same shape as every *TrainingService in Poink.ML (_lock/IsRunning/Error/
//  OnChanged, kicked off via fire-and-forget Task.Run) and as SimulationEngine
//  here, but drives RlEpisodeRunner in a tight loop instead of a UI-paced
//  Timer -- training needs to blast through many episodes fast. One
//  optimizer is created for the whole run (not per episode) so Adam's
//  momentum/variance state persists correctly across episodes.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Training;

internal sealed class RlTrainingService
{
    private const int RecentRewardWindow = 20;
    private const int MaxMetricsLog = 500;

    private readonly object _lock = new();
    private readonly ITickRepository _tickRepository;
    private readonly List<ISimulationBrain> _brains;
    private readonly List<IRlTrainingStrategy> _strategies;
    private readonly List<RlEpisodeMetric> _episodeMetrics = new();

    public RlTrainingService(
        ITickRepository tickRepository, IEnumerable<ISimulationBrain> brains, IEnumerable<IRlTrainingStrategy> strategies)
    {
        _tickRepository = tickRepository;
        _brains = brains.ToList();
        _strategies = strategies.ToList();
    }

    public IReadOnlyList<string> BrainNames => _brains.Select(b => b.Name).ToList();
    public IReadOnlyList<string> StrategyNames => _strategies.Select(s => s.Name).ToList();

    public bool IsRunning { get; private set; }
    public string? Error { get; private set; }
    public int EpisodesCompleted { get; private set; }
    public int TotalEpisodes { get; private set; }
    public double RecentAverageReward { get; private set; }
    public double BestReward { get; private set; }
    public double WorstReward { get; private set; }
    public RlActionCounts ActionCounts { get; private set; } = RlActionCounts.Empty;

    // Labeled snapshot of the most recent episode's final decision inputs --
    // null until the first episode with a describable (IPolicyGradientBrain)
    // brain completes. See RlEpisode's doc comment for why this is per-episode
    // rather than a per-tick stream.
    public IReadOnlyList<RlInputFeature>? LatestInputs { get; private set; }

    // Bounded log of every completed episode this run -- the toggleable
    // "visual feedback" the Train page renders as a table, most-recent-first.
    public IReadOnlyList<RlEpisodeMetric> RecentEpisodes
    {
        get { lock (_lock) { return _episodeMetrics.ToList(); } }
    }

    public event Action? OnChanged;

    public bool TryStartTraining(
        string brainName, string strategyName, int episodeCount, int ticksPerEpisode, double startingCash, double learningRate,
        RlDataSourceConfig dataSource)
    {
        var brain = _brains.FirstOrDefault(b => b.Name == brainName);
        var strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);

        lock (_lock)
        {
            if (IsRunning) return false;

            if (brain is null) { Error = $"no brain named '{brainName}'"; return false; }
            if (strategy is null) { Error = $"no training strategy named '{strategyName}'"; return false; }
            if (!strategy.CanTrain(brain)) { Error = $"'{strategy.Name}' cannot train '{brain.Name}'"; return false; }
            if (dataSource.UseHistoricData && string.IsNullOrWhiteSpace(dataSource.Table))
            {
                Error = "pick a symbol/table for historic data";
                return false;
            }

            IsRunning = true;
            Error = null;
            EpisodesCompleted = 0;
            TotalEpisodes = episodeCount;
            RecentAverageReward = 0;
            BestReward = 0;
            WorstReward = 0;
            ActionCounts = RlActionCounts.Empty;
            LatestInputs = null;
            _episodeMetrics.Clear();
        }
        OnChanged?.Invoke();

        _ = Task.Run(() =>
        {
            try
            {
                // Loaded once up front (like SimulationEngine.TryLoad) rather than
                // re-querying Postgres every episode -- a training run can be
                // hundreds of episodes, and ReadTicks is a lazy per-call iterator.
                List<Tick>? historicTicks = null;
                if (dataSource.UseHistoricData)
                {
                    historicTicks = _tickRepository.ReadTicks(dataSource.Table, dataSource.From, dataSource.To).ToList();
                    if (historicTicks.Count < ticksPerEpisode)
                        throw new InvalidOperationException(
                            $"'{dataSource.Table}' only has {historicTicks.Count} ticks in the selected range, need at least {ticksPerEpisode}");
                }

                var optimizer = strategy.CreateOptimizer(brain, learningRate);
                var recentRewards = new Queue<double>();
                var rng = new Random();

                for (int episode = 0; episode < episodeCount; episode++)
                {
                    var episodeTicks = historicTicks is not null
                        ? RandomWindow(historicTicks, ticksPerEpisode, rng)
                        : DummyTickGenerator.Generate(DateTime.UtcNow.AddHours(-4), DateTime.UtcNow, ticksPerEpisode);

                    var result = RlEpisodeRunner.Run(brain, episodeTicks, startingCash);
                    double reward = strategy.TrainEpisode(brain, optimizer, result);

                    recentRewards.Enqueue(reward);
                    if (recentRewards.Count > RecentRewardWindow) recentRewards.Dequeue();

                    lock (_lock)
                    {
                        EpisodesCompleted = episode + 1;
                        RecentAverageReward = recentRewards.Average();
                        BestReward = EpisodesCompleted == 1 ? reward : Math.Max(BestReward, reward);
                        WorstReward = EpisodesCompleted == 1 ? reward : Math.Min(WorstReward, reward);
                        ActionCounts = result.Steps.Aggregate(ActionCounts, (counts, step) => counts.Add(step.Action));
                        if (result.LastInputs is not null) LatestInputs = result.LastInputs;
                        _episodeMetrics.Add(new RlEpisodeMetric(episode + 1, reward, result.FinalCapital.Equity));
                        if (_episodeMetrics.Count > MaxMetricsLog) _episodeMetrics.RemoveAt(0);
                    }
                    OnChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                lock (_lock) { Error = ex.Message; }
            }
            finally
            {
                lock (_lock) { IsRunning = false; }
                OnChanged?.Invoke();
            }
        });

        return true;
    }

    // Picks a random contiguous slice of `count` ticks out of the full historic
    // range loaded for this run, so successive episodes train against different
    // (but real) market windows instead of replaying the same segment every time.
    private static List<Tick> RandomWindow(List<Tick> ticks, int count, Random rng)
    {
        int maxStart = ticks.Count - count;
        int start = maxStart <= 0 ? 0 : rng.Next(0, maxStart + 1);
        return ticks.GetRange(start, count);
    }
}
