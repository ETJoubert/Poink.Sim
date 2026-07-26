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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Training;

// One entry in the Brain picker -- IsNeuralNetwork is what lets the UI (see
// BrainSelect) visually separate trained/trainable brains from stateless
// heuristic ones, without the picker needing to know about
// IPolicyGradientBrain itself. Public because it's passed as a [Parameter]
// into that Razor component (see RlEpisodeMetric's doc comment for why).
public sealed record BrainOption(string Name, bool IsNeuralNetwork);

internal sealed class RlTrainingService
{
    private const int RecentRewardWindow = 20;
    private const int MaxMetricsLog = 500;

    // How many extra bars' worth of ticks to fetch beyond episodeBars -- a
    // safety margin so the aggregator reliably completes the *last* requested
    // bar even when the fetch window ends right at its boundary.
    private const int EpisodeSpanBufferBars = 1;

    // Synthetic episodes have no real tick density to match -- 1 tick/sec is
    // "good enough to exercise playback/aggregation/brain wiring" (see
    // DummyTickGenerator's own doc comment) without an unreasonably large list.
    private const double SyntheticTickIntervalSeconds = 1.0;

    private readonly object _lock = new();
    private readonly BrainRegistry _brainRegistry;
    private readonly List<IRlTrainingStrategy> _strategies;
    private readonly List<IRewardPolicy> _rewardPolicies;
    private readonly List<RlEpisodeMetric> _episodeMetrics = new();
    private readonly List<RlEpisodeTrades> _episodeTrades = new();
    private readonly List<RlEpisodeWindow> _episodeWindows = new();
    private readonly IBrainRepository _brainRepository;
    private readonly ITickRepository _tickRepository;
    private readonly Trader.ITraderFactory _traderFactory;
    private readonly ILogger<RlTrainingService> _logger;

    // Which saved snapshot (if any) a live brain's current weights actually
    // match -- keyed by brain architecture name. Set on load/save, cleared the
    // moment that brain starts training again (its weights immediately diverge
    // from whatever was last saved). Read by SimulationPage's Brain info card.
    private readonly Dictionary<string, long> _activeSnapshotId = new();

    // Pause/Stop signaling for the training loop below -- checked only between
    // episodes (there's no per-step checkpoint to interrupt mid-episode), so
    // both take effect once the in-flight episode finishes. Starts signaled
    // (open gate) so a fresh run never blocks; Pause closes the gate, Resume
    // or Stop reopens it so a paused loop can observe _stopRequested and exit
    // cleanly instead of blocking forever.
    private readonly ManualResetEventSlim _resumeSignal = new(true);
    private volatile bool _stopRequested;

    // Set for exactly one upcoming episode -- the training loop below checks
    // this right after that episode completes and, if set, auto-pauses (same
    // effect as PauseTraining) instead of continuing straight into the next
    // one. Drives StepTraining (advance one episode from an already-paused
    // run) and TryStartTraining's pauseAfterFirstEpisode (start a run already
    // stepped, for stepping through from the very first episode).
    private volatile bool _pauseAfterNextEpisode;

    public RlTrainingService(
        BrainRegistry brainRegistry, IEnumerable<IRlTrainingStrategy> strategies, IEnumerable<IRewardPolicy> rewardPolicies,
        IBrainRepository brainRepository, ITickRepository tickRepository, Trader.ITraderFactory traderFactory,
        ILogger<RlTrainingService> logger)
    {
        _brainRegistry = brainRegistry;
        _strategies = strategies.ToList();
        _rewardPolicies = rewardPolicies.ToList();
        _brainRepository = brainRepository;
        _tickRepository = tickRepository;
        _traderFactory = traderFactory;
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
                var brain = _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == latest.BrainName);
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
        _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == brainName)?.DescribeNetworkShape() ?? [];

    // Null for a brain with no profile-style trailing-bar window (e.g.
    // a pure Bar Window brain, or an unknown name) -- see IHasLookbackBars'
    // doc comment. Read by the Train page's episode replay to size its
    // "what range the trader sees" chart marker for brains this doesn't cover
    // (see BarWindowEncoder's own fixed, unexposed window instead).
    public int? GetLookbackBars(string brainName) =>
        (_brainRegistry.All.FirstOrDefault(b => b.Name == brainName) as IHasLookbackBars)?.LookbackBars;

    public IReadOnlyList<string> BrainNames => _brainRegistry.All.Select(b => b.Name).ToList();

    // NN vs non-NN is just "does this brain have trainable weights" --
    // IPolicyGradientBrain is the only family that does today (see that
    // interface's doc comment on why a heuristic brain never implements it).
    public IReadOnlyList<BrainOption> BrainOptions =>
        _brainRegistry.All.Select(b => new BrainOption(b.Name, b is IPolicyGradientBrain)).ToList();

    public IReadOnlyList<string> StrategyNames => _strategies.Select(s => s.Name).ToList();

    public IReadOnlyList<string> RewardPolicyNames => _rewardPolicies.Select(p => p.Name).ToList();

    // Cached, not re-queried on every render -- refreshed explicitly after a
    // save/delete (see RefreshSavedBrains) and once here at startup.
    public IReadOnlyList<SavedBrainSummary> SavedBrains { get; private set; } = [];

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
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

    // Same episode/decision as LatestInputs above -- what the network decided
    // from those inputs (see RlOutputFeature's doc comment), for the Train
    // page's "what the network decided" panel.
    public IReadOnlyList<RlOutputFeature>? LatestOutputs { get; private set; }

    // The most recently completed episode's full bar/histogram sequence --
    // overwritten every episode (unlike RecentEpisodes/RecentEpisodeTrades,
    // there's no bounded history of many episodes' worth here, just the
    // latest one) so the Train page's "current episode" bar chart and
    // volume/price profiles have something to show, paired well with the
    // STEP control for pausing on exactly one episode at a time.
    public IReadOnlyList<HlocAvdtBar> LatestEpisodeBars { get; private set; } = Array.Empty<HlocAvdtBar>();
    public IReadOnlyList<BarPriceHistogram> LatestEpisodeBarHistograms { get; private set; } = Array.Empty<BarPriceHistogram>();

    // How many of LatestEpisodeBars' leading entries are warm-up (history-
    // building, no decision -- see RlEpisode.WarmupBars) -- LatestEpisodeBars
    // [LatestEpisodeWarmupBars] is the first bar a decision was actually made
    // on. LatestEpisodeBarInputs/BarEquity/BarOutputs below cover only that
    // decision segment, so a caller indexing across both (e.g. TrainLiveTab's
    // episode replay) must offset by this. 0 for a brain with no windowed
    // encoder.
    public int LatestEpisodeWarmupBars { get; private set; }

    // Same episode as LatestEpisodeBars above (set together, every episode) --
    // its closed-trade log, for the Train page's episode replay to draw as
    // entry-to-exit lines on the bar chart as playback reveals each trade's
    // exit bar.
    public IReadOnlyList<Trade> LatestEpisodeTrades { get; private set; } = Array.Empty<Trade>();

    // Same episode as LatestEpisodeBars above, but only its decision bars --
    // index-aligned 1:1 with LatestEpisodeBars[LatestEpisodeWarmupBars..], not
    // LatestEpisodeBars itself (see LatestEpisodeWarmupBars above). Each entry
    // is what DescribeInputs saw as of that bar's decision, so the Train
    // page's episode replay can show "what the network saw" synced to
    // whichever decision bar is currently revealed, not just the episode's
    // last one (see LatestInputs above, which this doesn't replace).
    public IReadOnlyList<IReadOnlyList<RlInputFeature>> LatestEpisodeBarInputs { get; private set; } = Array.Empty<IReadOnlyList<RlInputFeature>>();

    // Index-aligned 1:1 with LatestEpisodeBarInputs (not LatestEpisodeBars --
    // both share the same IPolicyGradientBrain gate and LatestEpisodeWarmupBars
    // offset) -- each entry is that decision's pre-Apply equity, one of the
    // two anchors the Train page's de-normalized inputs toggle can invert a
    // feature against (see RlEpisode.BarEquity's doc comment).
    public IReadOnlyList<double> LatestEpisodeBarEquity { get; private set; } = Array.Empty<double>();

    // Same episode as LatestEpisodeBars above, index-aligned 1:1 with
    // LatestEpisodeBarInputs (same LatestEpisodeWarmupBars offset) -- each
    // entry is what the network decided as of that bar's decision (see
    // LatestOutputs above, which this doesn't replace, and RlOutputFeature's
    // doc comment).
    public IReadOnlyList<IReadOnlyList<RlOutputFeature>> LatestEpisodeBarOutputs { get; private set; } = Array.Empty<IReadOnlyList<RlOutputFeature>>();

    // Bounded log of every completed episode this run -- the toggleable
    // "visual feedback" the Train page renders as a table, most-recent-first.
    public IReadOnlyList<RlEpisodeMetric> RecentEpisodes
    {
        get { lock (_lock) { return _episodeMetrics.ToList(); } }
    }

    // Same bounded window as RecentEpisodes above, pairing each entry's full
    // trade log/equity extremes with its episode number -- lets the Train page
    // derive TradeStatistics per episode, or pooled across this whole window
    // as a "session" rollup (see TrainLiveTab's EpisodeTraderStatsRows/
    // SessionTraderStatsRows).
    public IReadOnlyList<RlEpisodeTrades> RecentEpisodeTrades
    {
        get { lock (_lock) { return _episodeTrades.ToList(); } }
    }

    // Same bounded window as RecentEpisodes above, but only populated for a
    // historic run (see TryStartTraining's loop below) -- a synthetic/dummy
    // run has no real data range to plot against, so this stays empty for
    // one. Feeds the Train page's data-coverage timeline (see
    // EpisodeCoverageTimeline), which shades each bars/episode-length block
    // of the selected range by how many of these windows overlap it.
    public IReadOnlyList<RlEpisodeWindow> RecentEpisodeWindows
    {
        get { lock (_lock) { return _episodeWindows.ToList(); } }
    }

    // Set by SaveBrain(); read by the Train page to show what happened after
    // clicking "Save Model". Separate from Error, which is only ever set by a
    // training run itself.
    public string? SaveStatus { get; private set; }

    public event Action? OnChanged;

    // historicRange identifies a table + time range to sample from -- each
    // episode pulls its own bounded window straight from Postgres via
    // ITickRepository.ReadTicks (chunk-pruned by Timescale), so nothing here
    // ever materializes the full range. Null means synthetic random-walk
    // episodes. pauseAfterFirstEpisode starts the run already stepped (see
    // StepTraining) -- lets a user step through from episode 1 without first
    // running a normal Start.
    public bool TryStartTraining(
        string brainName, string strategyName, string rewardPolicyName, int episodeCount, int episodeBars, int barWidthMinutes, double startingCash,
        double learningRate, int lookbackBars, (string Table, DateTime From, DateTime To)? historicRange,
        Trader.TraderSettings? traderSettings = null, bool pauseAfterFirstEpisode = false)
    {
        var brain = _brainRegistry.All.FirstOrDefault(b => b.Name == brainName);
        var strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);
        var rewardPolicy = _rewardPolicies.FirstOrDefault(p => p.Name == rewardPolicyName);

        lock (_lock)
        {
            if (IsRunning) return false;

            if (brain is null) { Error = $"no brain named '{brainName}'"; return false; }
            if (strategy is null) { Error = $"no training strategy named '{strategyName}'"; return false; }
            if (rewardPolicy is null) { Error = $"no reward policy named '{rewardPolicyName}'"; return false; }
            if (!strategy.CanTrain(brain)) { Error = $"'{strategy.Name}' cannot train '{brain.Name}'"; return false; }

            IsRunning = true;
            IsPaused = false;
            _stopRequested = false;
            _resumeSignal.Set();
            Error = null;
            EpisodesCompleted = 0;
            TotalEpisodes = episodeCount;
            RecentAverageReward = 0;
            BestReward = 0;
            WorstReward = 0;
            ActionCounts = RlActionCounts.Empty;
            LatestInputs = null;
            LatestOutputs = null;
            LatestEpisodeBars = Array.Empty<HlocAvdtBar>();
            LatestEpisodeBarHistograms = Array.Empty<BarPriceHistogram>();
            LatestEpisodeWarmupBars = 0;
            LatestEpisodeTrades = Array.Empty<Trade>();
            LatestEpisodeBarInputs = Array.Empty<IReadOnlyList<RlInputFeature>>();
            LatestEpisodeBarEquity = Array.Empty<double>();
            LatestEpisodeBarOutputs = Array.Empty<IReadOnlyList<RlOutputFeature>>();
            _episodeMetrics.Clear();
            _episodeTrades.Clear();
            _episodeWindows.Clear();
            _pauseAfterNextEpisode = pauseAfterFirstEpisode;
            _activeSnapshotId.Remove(brain.Name);
        }

        // See IPolicyGradientBrain's device-placement doc comment -- safe here
        // because IsRunning above already rejects a second concurrent run on
        // this same brain, and this app's usage is train-then-simulate, never
        // simulating this brain while it's mid-training.
        if (brain is IPolicyGradientBrain trainableBrain)
            trainableBrain.MoveToTrainingDevice();

        // No-op for brains built from encoders with no lookback window (e.g. a
        // bare Bar Window brain) -- only IHasLookbackBars brains (composed
        // with a VolumeProfileEncoder and/or TimeProfileEncoder part) read
        // this. Live Simulation playback reuses whatever was last set here.
        if (brain is IHasLookbackBars lookbackBrain)
            lookbackBrain.LookbackBars = lookbackBars;

        // Read after the LookbackBars assignment above so a lookback-windowed
        // brain's WarmupBars already reflects this run's configured value (see
        // ComposedPolicyBrain.WarmupBars / VolumeProfileEncoder.WarmupBars). 0
        // for a brain with no windowed encoder (e.g. a bare TraderStateEncoder
        // brain) or a non-policy-gradient brain.
        int warmupBars = brain is IPolicyGradientBrain warmupAwareBrain ? warmupAwareBrain.WarmupBars : 0;

        OnChanged?.Invoke();

        string source = historicRange is { } r ? r.Table : "dummy";
        _logger.LogInformation(
            "Training started: brain={Brain}, strategy={Strategy}, rewardPolicy={RewardPolicy}, episodes={EpisodeCount}, episodeBars={EpisodeBars}, warmupBars={WarmupBars}, barWidthMinutes={BarWidthMinutes}, source={Source}",
            brainName, strategyName, rewardPolicyName, episodeCount, episodeBars, warmupBars, barWidthMinutes, source);

        var barWidth = TimeSpan.FromMinutes(barWidthMinutes);
        // +warmupBars so the brain's lookback window is full of real episode
        // data by the time its first decision happens (see RlEpisodeRunner's
        // warmupBars doc comment) -- never zero-padded/undersized history.
        var fetchSpan = barWidth * (episodeBars + warmupBars + EpisodeSpanBufferBars);

        _ = Task.Run(() =>
        {
            try
            {
                if (historicRange is { } range)
                {
                    if (range.To - range.From < fetchSpan)
                        throw new InvalidOperationException(
                            $"selected range only spans {(range.To - range.From).TotalMinutes:N0} min, need at least " +
                            $"{fetchSpan.TotalMinutes:N0} min for a {episodeBars}-bar episode at {barWidthMinutes}m bars" +
                            (warmupBars > 0 ? $" (plus {warmupBars} warm-up bars)" : ""));
                }

                var optimizer = strategy.CreateOptimizer(brain, learningRate);
                var recentRewards = new Queue<double>();
                var rng = new Random();
                int syntheticTickCount = Math.Max(2, (int)(fetchSpan.TotalSeconds / SyntheticTickIntervalSeconds));

                for (int episode = 0; episode < episodeCount; episode++)
                {
                    _resumeSignal.Wait();
                    if (_stopRequested) break;

                    var episodeTicks = historicRange is { } episodeRange
                        ? RandomEpisodeWindow(episodeRange.Table, episodeRange.From, episodeRange.To, fetchSpan, rng)
                        : DummyTickGenerator.Generate(DateTime.UtcNow - fetchSpan, DateTime.UtcNow, syntheticTickCount);

                    var result = RlEpisodeRunner.Run(
                        brain, episodeTicks, startingCash, episodeBars, barWidth, _traderFactory,
                        settings: traderSettings, warmupBars: warmupBars, rewardPolicy: rewardPolicy);
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
                        if (result.LastOutputs is not null) LatestOutputs = result.LastOutputs;
                        LatestEpisodeBars = result.Bars;
                        LatestEpisodeBarHistograms = result.BarHistograms;
                        LatestEpisodeWarmupBars = result.WarmupBars;
                        LatestEpisodeTrades = result.Trades;
                        LatestEpisodeBarInputs = result.BarInputs;
                        LatestEpisodeBarEquity = result.BarEquity;
                        LatestEpisodeBarOutputs = result.BarOutputs;
                        _episodeMetrics.Add(new RlEpisodeMetric(episode + 1, reward, result.FinalCapital.Equity));
                        if (_episodeMetrics.Count > MaxMetricsLog) _episodeMetrics.RemoveAt(0);
                        _episodeTrades.Add(new RlEpisodeTrades(episode + 1, result.Trades, result.MaxEquity, result.MaxDrawdown));
                        if (_episodeTrades.Count > MaxMetricsLog) _episodeTrades.RemoveAt(0);
                        if (historicRange is not null)
                        {
                            _episodeWindows.Add(new RlEpisodeWindow(episode + 1, result.WindowStart, result.WindowEnd));
                            if (_episodeWindows.Count > MaxMetricsLog) _episodeWindows.RemoveAt(0);
                        }
                    }
                    OnChanged?.Invoke();

                    // Consume the one-shot step flag right after the episode it
                    // was requested for -- same effect as PauseTraining, so the
                    // loop's next _resumeSignal.Wait() blocks until Resume or
                    // another StepTraining call reopens the gate.
                    if (_pauseAfterNextEpisode)
                    {
                        _pauseAfterNextEpisode = false;
                        lock (_lock)
                        {
                            if (IsRunning && !_stopRequested) { IsPaused = true; _resumeSignal.Reset(); }
                        }
                        OnChanged?.Invoke();
                    }
                }

                if (_stopRequested)
                {
                    _logger.LogInformation(
                        "Training stopped by user: brain={Brain}, episodesCompleted={EpisodesCompleted}",
                        brainName, EpisodesCompleted);
                }
                else
                {
                    _logger.LogInformation(
                        "Training completed: brain={Brain}, episodes={EpisodesCompleted}, bestReward={BestReward}, recentAvgReward={RecentAverageReward}",
                        brainName, EpisodesCompleted, BestReward, RecentAverageReward);
                }
            }
            catch (Exception ex)
            {
                lock (_lock) { Error = ex.Message; }
                _logger.LogError(ex, "Training failed: brain={Brain}, strategy={Strategy}", brainName, strategyName);
            }
            finally
            {
                lock (_lock) { IsRunning = false; IsPaused = false; }
                OnChanged?.Invoke();
            }
        });

        return true;
    }

    // Closes the resume gate so the loop above blocks before starting its
    // next episode. No-op if nothing is running or it's already paused.
    public void PauseTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || IsPaused) return;
            IsPaused = true;
            _resumeSignal.Reset();
        }
        OnChanged?.Invoke();
    }

    public void ResumeTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || !IsPaused) return;
            IsPaused = false;
            _resumeSignal.Set();
        }
        OnChanged?.Invoke();
    }

    // Reopens the gate for exactly one episode, then the training loop's own
    // _pauseAfterNextEpisode check re-closes it -- lets a user advance a
    // paused run one episode at a time instead of Resume's free-run. No-op
    // unless a run is currently paused (see TryStartTraining's
    // pauseAfterFirstEpisode for stepping from a not-yet-started run).
    public void StepTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || !IsPaused) return;
            IsPaused = false;
            _pauseAfterNextEpisode = true;
            _resumeSignal.Set();
        }
        OnChanged?.Invoke();
    }

    // Reopens the resume gate (in case it was paused, so the loop can
    // actually observe _stopRequested instead of blocking forever) and asks
    // the loop to exit after its in-flight episode -- IsRunning only flips
    // back to false once that happens, via the loop's own finally block.
    public void StopTraining()
    {
        lock (_lock)
        {
            if (!IsRunning) return;
            _stopRequested = true;
            IsPaused = false;
            _resumeSignal.Set();
        }
        OnChanged?.Invoke();
    }

    // Builds a new ComposedPolicyBrain from the Train page's Brain Builder
    // panel and adds it to the shared BrainRegistry -- ephemeral, same as
    // every other brain's *shape* (only trained weights persist across a
    // restart, via SaveBrain below). TraderStateEncoder is always appended
    // regardless of encoderKinds (see BrainCatalog's doc comment); the caller
    // (TrainPage.razor.cs) is responsible for listing encoderKinds in
    // canonical order (BarWindow before VolumeProfile/TimeProfile) so
    // CompositeEncoder's sequence-first guard is satisfied.
    public bool TryBuildBrain(string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Error = "enter a name for the new brain";
            OnChanged?.Invoke();
            return false;
        }
        if (_brainRegistry.All.Any(b => b.Name == name))
        {
            Error = $"a brain named '{name}' already exists";
            OnChanged?.Invoke();
            return false;
        }
        if (encoderKinds.Count == 0)
        {
            Error = "pick at least one encoder";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            var parts = encoderKinds.Select(BrainCatalog.CreateEncoder).Append(new TraderStateEncoder()).ToArray();
            var architecture = BrainCatalog.CreateArchitecture(architectureKind, hiddenSizes);
            var brain = ComposedPolicyBrain.Create(name, architecture, parts);
            _brainRegistry.Register(brain);
            Error = null;
            OnChanged?.Invoke();
            _logger.LogInformation("Built new brain: name={Name}, encoders={Encoders}, architecture={Architecture}",
                name, string.Join(",", encoderKinds), architectureKind);
            return true;
        }
        catch (Exception ex)
        {
            Error = $"couldn't build brain: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to build brain: name={Name}", name);
            return false;
        }
    }

    // Exports the named brain's live weights and inserts a new snapshot row
    // (name/strategy/parameters travel with it -- see IBrainRepository's doc
    // comment on why this is always an insert, never an upsert). Safe to call
    // while a run isn't in progress -- reading the brain's own Network is
    // what's serialized, so nothing here needs the training loop's lock.
    public bool SaveBrain(
        string savedName, string brainName, string strategyName, string dataset,
        int episodeCount, int episodeBars, int barWidthMinutes, double startingCash, double learningRate,
        int lookbackBars, Trader.TraderSettings? traderSettings = null)
    {
        var brain = _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == brainName);
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
            var settings = traderSettings ?? new Trader.TraderSettings();
            var parameters = new BrainTrainingParameters
            {
                EpisodeCount = episodeCount, EpisodeBars = episodeBars, BarWidthMinutes = barWidthMinutes,
                StartingCash = startingCash, LearningRate = learningRate,
                LookbackBars = brain is IHasLookbackBars ? lookbackBars : null,
                Leverage = settings.Leverage, StopOutLevelPercent = settings.StopOutLevelPercent,
                CommissionRatePercent = settings.CommissionModel is Trader.Commission.PercentOfNotionalCommission p ? p.RatePercent : 0,
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

        var brain = _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == summary.BrainName);
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

    // Picks a random start timestamp within [from, to - fetchSpan] and returns
    // a still-lazy stream over that fetchSpan window (chunk-pruned, see
    // ITickRepository.ReadTicks) -- deliberately NOT materialized here.
    // RlEpisodeRunner.Run stops enumerating as soon as episodeBars bars
    // complete, and since ReadTicks is a lazy Dapper query (buffered: false),
    // stopping early actually cuts the Postgres read short instead of having
    // already pulled the whole (possibly much larger, for a dense table)
    // fetchSpan window into memory only to use a fraction of it.
    //
    // The gap check below (.Any()) costs one extra cheap, chunk-pruned round
    // trip per attempt -- negligible next to what materializing the full
    // window used to cost -- so successive episodes still train against
    // different (but real) market windows without ever holding a full range,
    // or even a full fetchSpan window, in memory. Retries a few times if the
    // random start lands in a gap with no ticks (e.g. a weekend) before
    // falling back to reading from the range start, which TryStartTraining's
    // upfront duration check already guarantees has at least `fetchSpan` of
    // range ahead of it... other than gaps, so this fallback can still
    // legitimately come up short near a gap-heavy `to`.
    private IEnumerable<Tick> RandomEpisodeWindow(string table, DateTime from, DateTime to, TimeSpan fetchSpan, Random rng)
    {
        var latestStart = to - fetchSpan;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var start = latestStart <= from ? from : from.AddTicks((long)(rng.NextDouble() * (latestStart - from).Ticks));
            if (_tickRepository.ReadTicks(table, start, start + fetchSpan).Any())
                return _tickRepository.ReadTicks(table, start, start + fetchSpan);
        }
        _logger.LogWarning(
            "RandomEpisodeWindow: 5 random starts in {Table} landed in empty windows; falling back to range start {From}",
            table, from);
        return _tickRepository.ReadTicks(table, from, from + fetchSpan);
    }
}
