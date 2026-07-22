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
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Training;

// One entry in the Brain picker -- IsNeuralNetwork is what lets the UI (see
// BrainStrategyPicker) visually separate trained/trainable brains from
// stateless heuristic ones, without the picker needing to know about
// IPolicyGradientBrain itself. Public because it's passed as a [Parameter]
// into that Razor component (see RlEpisodeMetric's doc comment for why).
public sealed record BrainOption(string Name, bool IsNeuralNetwork);

internal sealed class RlTrainingService
{
    private const int RecentRewardWindow = 20;
    private const int MaxMetricsLog = 500;

    private readonly object _lock = new();
    private readonly List<ISimulationBrain> _brains;
    private readonly List<IRlTrainingStrategy> _strategies;
    private readonly List<RlEpisodeMetric> _episodeMetrics = new();
    private readonly IBrainRepository _brainRepository;
    private readonly ITickRepository _tickRepository;
    private readonly ILogger<RlTrainingService> _logger;

    // Which saved snapshot (if any) a live brain's current weights actually
    // match -- keyed by brain architecture name. Set on load/save, cleared the
    // moment that brain starts training again (its weights immediately diverge
    // from whatever was last saved). Read by SimulationPage's Brain info card.
    private readonly Dictionary<string, long> _activeSnapshotId = new();

    public RlTrainingService(
        IEnumerable<ISimulationBrain> brains, IEnumerable<IRlTrainingStrategy> strategies,
        IBrainRepository brainRepository, ITickRepository tickRepository, ILogger<RlTrainingService> logger)
    {
        _brains = brains.ToList();
        _strategies = strategies.ToList();
        _brainRepository = brainRepository;
        _tickRepository = tickRepository;
        _logger = logger;

        // Restore whatever was last saved for each trainable brain -- these are
        // DI singletons that otherwise only keep weights in memory, so without
        // this every process restart would silently throw away prior training.
        // Postgres being unreachable at startup (or a brain never having been
        // saved) shouldn't stop the app from coming up untrained.
        try
        {
            SavedBrains = _brainRepository.ListBrains();
            foreach (var latest in SavedBrains.GroupBy(b => b.BrainName).Select(g => g.OrderByDescending(b => b.SavedAt).First()))
            {
                var brain = _brains.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == latest.BrainName);
                if (brain is null) continue;
                var weights = _brainRepository.LoadWeights(latest.Id);
                if (weights is null) continue;
                brain.ImportWeights(weights);
                _activeSnapshotId[latest.BrainName] = latest.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore saved brain weights at startup; continuing with untrained brains");
        }
    }

    // Null if this brain's live weights don't (or no longer) match any saved
    // snapshot -- e.g. it's never been saved, or it's been trained further
    // since the last save/load.
    public SavedBrainSummary? GetActiveSnapshot(string brainName) =>
        _activeSnapshotId.TryGetValue(brainName, out var id) ? SavedBrains.FirstOrDefault(b => b.Id == id) : null;

    // Empty for an unknown or non-neural brain name -- see
    // IPolicyGradientBrain.DescribeNetworkShape for what feeds the Train
    // page's Network Activity animation.
    public IReadOnlyList<NetworkLayerShape> DescribeNetworkShape(string brainName) =>
        _brains.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == brainName)?.DescribeNetworkShape() ?? [];

    public IReadOnlyList<string> BrainNames => _brains.Select(b => b.Name).ToList();

    // NN vs non-NN is just "does this brain have trainable weights" --
    // IPolicyGradientBrain is the only family that does today (see that
    // interface's doc comment on why a heuristic brain never implements it).
    public IReadOnlyList<BrainOption> BrainOptions =>
        _brains.Select(b => new BrainOption(b.Name, b is IPolicyGradientBrain)).ToList();

    public IReadOnlyList<string> StrategyNames => _strategies.Select(s => s.Name).ToList();

    // Cached, not re-queried on every render -- refreshed explicitly after a
    // save/delete (see RefreshSavedBrains) and once here at startup.
    public IReadOnlyList<SavedBrainSummary> SavedBrains { get; private set; } = [];

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

    // Set by SaveBrain(); read by the Train page to show what happened after
    // clicking "Save Model". Separate from Error, which is only ever set by a
    // training run itself.
    public string? SaveStatus { get; private set; }

    public event Action? OnChanged;

    // historicRange identifies a table + time range to sample from -- each
    // episode pulls its own bounded window straight from Postgres via
    // ITickRepository.ReadWindow (chunk-pruned by Timescale), so nothing here
    // ever materializes the full range. Null means synthetic random-walk
    // episodes.
    public bool TryStartTraining(
        string brainName, string strategyName, int episodeCount, int ticksPerEpisode, double startingCash, double learningRate,
        (string Table, DateTime From, DateTime To)? historicRange)
    {
        var brain = _brains.FirstOrDefault(b => b.Name == brainName);
        var strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);

        lock (_lock)
        {
            if (IsRunning) return false;

            if (brain is null) { Error = $"no brain named '{brainName}'"; return false; }
            if (strategy is null) { Error = $"no training strategy named '{strategyName}'"; return false; }
            if (!strategy.CanTrain(brain)) { Error = $"'{strategy.Name}' cannot train '{brain.Name}'"; return false; }

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
            _activeSnapshotId.Remove(brain.Name);
        }

        // See IPolicyGradientBrain's device-placement doc comment -- safe here
        // because IsRunning above already rejects a second concurrent run on
        // this same brain, and this app's usage is train-then-simulate, never
        // simulating this brain while it's mid-training.
        if (brain is IPolicyGradientBrain trainableBrain)
            trainableBrain.MoveToTrainingDevice();

        OnChanged?.Invoke();

        string source = historicRange is { } r ? r.Table : "dummy";
        _logger.LogInformation(
            "Training started: brain={Brain}, strategy={Strategy}, episodes={EpisodeCount}, ticksPerEpisode={TicksPerEpisode}, source={Source}",
            brainName, strategyName, episodeCount, ticksPerEpisode, source);

        _ = Task.Run(() =>
        {
            try
            {
                if (historicRange is { } range)
                {
                    long available = _tickRepository.CountTicks(range.Table, range.From, range.To);
                    if (available < ticksPerEpisode)
                        throw new InvalidOperationException(
                            $"only {available} ticks in the selected range, need at least {ticksPerEpisode}");
                }

                var optimizer = strategy.CreateOptimizer(brain, learningRate);
                var recentRewards = new Queue<double>();
                var rng = new Random();

                for (int episode = 0; episode < episodeCount; episode++)
                {
                    var episodeTicks = historicRange is { } episodeRange
                        ? RandomEpisodeWindow(episodeRange.Table, episodeRange.From, episodeRange.To, ticksPerEpisode, rng)
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

                _logger.LogInformation(
                    "Training completed: brain={Brain}, episodes={EpisodesCompleted}, bestReward={BestReward}, recentAvgReward={RecentAverageReward}",
                    brainName, EpisodesCompleted, BestReward, RecentAverageReward);
            }
            catch (Exception ex)
            {
                lock (_lock) { Error = ex.Message; }
                _logger.LogError(ex, "Training failed: brain={Brain}, strategy={Strategy}", brainName, strategyName);
            }
            finally
            {
                lock (_lock) { IsRunning = false; }
                OnChanged?.Invoke();
            }
        });

        return true;
    }

    // Exports the named brain's live weights and inserts a new snapshot row
    // (name/strategy/parameters travel with it -- see IBrainRepository's doc
    // comment on why this is always an insert, never an upsert). Safe to call
    // while a run isn't in progress -- reading the brain's own Network is
    // what's serialized, so nothing here needs the training loop's lock.
    public bool SaveBrain(
        string savedName, string brainName, string strategyName, string dataset,
        int episodeCount, int ticksPerEpisode, double startingCash, double learningRate)
    {
        var brain = _brains.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == brainName);
        if (brain is null)
        {
            SaveStatus = $"'{brainName}' has no weights to save";
            OnChanged?.Invoke();
            return false;
        }
        if (string.IsNullOrWhiteSpace(savedName))
        {
            SaveStatus = "enter a name to save under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            var weights = brain.ExportWeights();
            var parameters = new BrainTrainingParameters
            {
                EpisodeCount = episodeCount, TicksPerEpisode = ticksPerEpisode,
                StartingCash = startingCash, LearningRate = learningRate,
            };
            long id = _brainRepository.SaveBrain(savedName, brainName, strategyName, dataset, parameters, brain.WeightCount(), RecentAverageReward, weights);
            _activeSnapshotId[brainName] = id;
            SaveStatus = $"saved '{savedName}' at {DateTime.Now:HH:mm:ss}";
            RefreshSavedBrains();
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"save failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to save brain snapshot: name={SavedName}, brain={BrainName}", savedName, brainName);
            return false;
        }
    }

    public void RefreshSavedBrains()
    {
        try
        {
            SavedBrains = _brainRepository.ListBrains();
        }
        catch (Exception ex)
        {
            SaveStatus = $"couldn't list saved brains: {ex.Message}";
            _logger.LogWarning(ex, "Failed to refresh saved brains list");
        }
        OnChanged?.Invoke();
    }

    // Imports a saved snapshot's weights into the live brain of the matching
    // architecture (see IPolicyGradientBrain's doc comment on why weights are
    // mutated in place rather than swapping in a new instance) -- this is
    // also how a saved brain gets used for Simulation: SimulationPage/
    // SimulationEngine already pick brains by this same live-singleton name.
    public bool LoadSavedBrain(long id)
    {
        var summary = SavedBrains.FirstOrDefault(b => b.Id == id);
        if (summary is null)
        {
            SaveStatus = "brain no longer exists";
            OnChanged?.Invoke();
            _logger.LogWarning("LoadSavedBrain: id {Id} no longer exists", id);
            return false;
        }

        var brain = _brains.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == summary.BrainName);
        if (brain is null)
        {
            SaveStatus = $"no live '{summary.BrainName}' brain to load into";
            OnChanged?.Invoke();
            _logger.LogWarning("LoadSavedBrain: no live '{BrainName}' brain to load id {Id} into", summary.BrainName, id);
            return false;
        }

        try
        {
            var weights = _brainRepository.LoadWeights(id);
            if (weights is null) { SaveStatus = "weights not found"; return false; }
            brain.ImportWeights(weights);
            _activeSnapshotId[summary.BrainName] = id;
            SaveStatus = $"loaded '{summary.Name}' into '{summary.BrainName}' -- ready to use in Simulation";
            _logger.LogInformation("Loaded saved brain {SavedName} (id={Id}) into live brain {BrainName}", summary.Name, id, summary.BrainName);
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"load failed: {ex.Message}";
            _logger.LogError(ex, "Failed to load saved brain id {Id}", id);
            return false;
        }
        finally
        {
            OnChanged?.Invoke();
        }
    }

    public bool DeleteSavedBrain(long id)
    {
        try
        {
            _brainRepository.DeleteBrain(id);
            SaveStatus = "deleted";
            RefreshSavedBrains();
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"delete failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to delete saved brain id {Id}", id);
            return false;
        }
    }

    // Picks a random start timestamp within [from, to) and reads up to `count`
    // ticks from there, so successive episodes train against different (but
    // real) market windows without ever holding the full range in memory.
    // Retries a few times if the random start lands in a gap with no ticks
    // (e.g. a weekend) before falling back to reading from the range start,
    // which TryStartTraining's upfront CountTicks check already guarantees
    // has at least `count` ticks ahead of it... other than gaps, so this
    // fallback can still legitimately come up short near a gap-heavy `to`.
    private List<Tick> RandomEpisodeWindow(string table, DateTime from, DateTime to, int count, Random rng)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var start = from.AddTicks((long)(rng.NextDouble() * (to - from).Ticks));
            var window = _tickRepository.ReadWindow(table, start, count);
            if (window.Count > 0) return window;
        }
        _logger.LogWarning(
            "RandomEpisodeWindow: 5 random starts in {Table} landed in empty windows; falling back to range start {From}",
            table, from);
        return _tickRepository.ReadWindow(table, from, count);
    }
}
