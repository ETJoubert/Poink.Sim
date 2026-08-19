
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Training;

internal sealed record RlTrainingLogEntry(DateTime TimestampUtc, string Level, string Message);

internal sealed class RlTrainingService : IRlTrainingService
{
    private const int RecentRewardWindow = 20;
    private const int MaxMetricsLog = 500;
    private const int MaxLogEntries = 200;


    private const int EpisodeLogInterval = 10;


    private const int EpisodeSpanBufferBars = 1;


    private const int PrefetchDepth = 3;

    private const double SyntheticTickIntervalSeconds = 1.0;

    private readonly object _lock = new();
    private readonly BrainRegistry _brainRegistry;
    private readonly List<IRlTrainingStrategy> _strategies;
    private readonly IRewardPolicyService _rewardPolicyService;
    private readonly List<RlEpisodeMetric> _episodeMetrics = new();
    private readonly List<RlEpisodeTrades> _episodeTrades = new();
    private readonly List<RlEpisodeWindow> _episodeWindows = new();
    private readonly RingBuffer<RlTrainingLogEntry> _trainingLog = new(MaxLogEntries);
    private readonly IModelService _modelService;
    private readonly IBrainService _brainService;
    private readonly ITickRepository _tickRepository;
    private readonly Trader.ITraderFactory _traderFactory;
    private readonly ILogger<RlTrainingService> _logger;


    private readonly Dictionary<string, long> _activeSnapshotId = new();


    private readonly Dictionary<string, int> _episodesTrainedByBrain = new();

    private readonly Dictionary<string, DateTime> _lastTrainedAt = new();


    private readonly ManualResetEventSlim _resumeSignal = new(true);
    private volatile bool _stopRequested;


    private volatile bool _pauseAfterNextEpisode;

    public RlTrainingService(
        BrainRegistry brainRegistry, IEnumerable<IRlTrainingStrategy> strategies, IRewardPolicyService rewardPolicyService,
        IModelService modelService, IBrainService brainService, ITickRepository tickRepository,
        Trader.ITraderFactory traderFactory, ILogger<RlTrainingService> logger)
    {
        _brainRegistry = brainRegistry;
        _strategies = strategies.ToList();
        _rewardPolicyService = rewardPolicyService;
        _modelService = modelService;
        _brainService = brainService;
        _tickRepository = tickRepository;
        _traderFactory = traderFactory;
        _logger = logger;

        _brainService.BrainReset += name =>
        {
            _episodesTrainedByBrain.Remove(name);
            _lastTrainedAt.Remove(name);
            _activeSnapshotId.Remove(name);
        };

        foreach (var latest in _modelService.Models.GroupBy(m => m.BrainName).Select(g => g.OrderByDescending(m => m.SavedAt).First()))
        {
            var brain = _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == latest.BrainName);
            if (brain is null)
                continue;
            try
            {
                var weights = _modelService.LoadWeights(latest.Id);
                if (weights is null)
                    continue;
                brain.ImportWeights(weights);
                _activeSnapshotId[latest.BrainName] = latest.Id;
                _episodesTrainedByBrain[latest.BrainName] = latest.Parameters.EpisodeCount;
                if (latest.TrainedAt is { } trainedAt)
                    _lastTrainedAt[latest.BrainName] = trainedAt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore saved brain weights for {BrainName}; continuing with untrained brain", latest.BrainName);
            }
        }
    }

    public TrainedModelSummary? GetActiveSnapshot(string brainName) =>
        _activeSnapshotId.TryGetValue(brainName, out var id) ? _modelService.Models.FirstOrDefault(m => m.Id == id) : null;

    public IReadOnlyList<string> StrategyNames => _strategies.Select(s => s.Name).ToList();

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
    public string? Error { get; private set; }
    public int EpisodesCompleted { get; private set; }
    public int TotalEpisodes { get; private set; }
    public double RecentAverageReward { get; private set; }
    public double BestReward { get; private set; }
    public double WorstReward { get; private set; }


    public double EpisodesPerSecond { get; private set; }
    public RlActionCounts ActionCounts { get; private set; } = RlActionCounts.Empty;


    public IReadOnlyList<RlInputFeature>? LatestInputs { get; private set; }


    public IReadOnlyList<RlOutputFeature>? LatestOutputs { get; private set; }


    public IReadOnlyList<HlocAvdtBar> LatestEpisodeBars { get; private set; } = Array.Empty<HlocAvdtBar>();
    public IReadOnlyList<BarPriceHistogram> LatestEpisodeBarHistograms { get; private set; } = Array.Empty<BarPriceHistogram>();


    public int LatestEpisodeWarmupBars { get; private set; }

    public SimulationSteppingMode LatestEpisodeSteppingMode { get; private set; }

    public double LatestEpisodeCellWidth { get; private set; }


    public IReadOnlyList<Trade> LatestEpisodeTrades { get; private set; } = Array.Empty<Trade>();


    public IReadOnlyList<IReadOnlyList<RlInputFeature>> LatestEpisodeBarInputs { get; private set; } = Array.Empty<IReadOnlyList<RlInputFeature>>();


    public IReadOnlyList<double> LatestEpisodeBarEquity { get; private set; } = Array.Empty<double>();

    public IReadOnlyList<IReadOnlyList<RlOutputFeature>> LatestEpisodeBarOutputs { get; private set; } = Array.Empty<IReadOnlyList<RlOutputFeature>>();


    public IReadOnlyList<RlBarAction> LatestEpisodeBarActions { get; private set; } = Array.Empty<RlBarAction>();


    public IReadOnlyList<RlEpisodeMetric> RecentEpisodes
    {
        get { lock (_lock) { return _episodeMetrics.ToList(); } }
    }


    public IReadOnlyList<RlEpisodeTrades> RecentEpisodeTrades
    {
        get { lock (_lock) { return _episodeTrades.ToList(); } }
    }


    public IReadOnlyList<RlEpisodeWindow> RecentEpisodeWindows
    {
        get { lock (_lock) { return _episodeWindows.ToList(); } }
    }

    public string? SaveStatus { get; private set; }


    public IReadOnlyList<RlTrainingLogEntry> RecentLog
    {
        get { lock (_lock) { return _trainingLog.ToList(); } }
    }

    public event Action? OnChanged;


    private void AppendLog(string level, string message)
    {
        lock (_lock)
        { _trainingLog.Add(new RlTrainingLogEntry(DateTime.UtcNow, level, message)); }
    }


    public bool TryStartTraining(
        string brainName, string strategyName, int episodeCount, int episodeBars, int barWidthSeconds, double startingCash,
        double learningRate, (string Table, DateTime From, DateTime To)? historicRange,
        Trader.TraderSettings? traderSettings = null, bool pauseAfterFirstEpisode = false,
        EpisodeTimeConstraint? episodeTimeConstraint = null,
        SimulationSteppingMode steppingMode = SimulationSteppingMode.Bar, double cellWidth = 0.10)
    {
        var timeConstraint = episodeTimeConstraint ?? EpisodeTimeConstraint.None;
        var brain = _brainRegistry.All.FirstOrDefault(b => b.Name == brainName);


        if (brain is null)
        {
            var network = _brainService.Brains.FirstOrDefault(n => n.Name == brainName);
            if (network is not null)
            {
                try
                {
                    brain = _brainService.BuildAndRegister(network.Name, network.Recipe);
                    _logger.LogInformation("Rebuilt brain '{Name}' from its saved network at training start", network.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to rebuild brain '{Name}' from its saved network at training start", network.Name);
                }
            }
        }

        var strategy = _strategies.FirstOrDefault(s => s.Name == strategyName);
        var rewardPolicy = _rewardPolicyService.SelectedPolicy;

        lock (_lock)
        {
            if (IsRunning)
                return false;

            if (brain is null)
            { Error = $"no brain named '{brainName}'"; return false; }
            if (strategy is null)
            { Error = $"no training strategy named '{strategyName}'"; return false; }
            if (!strategy.CanTrain(brain))
            { Error = $"'{strategy.Name}' cannot train '{brain.Name}'"; return false; }

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
            EpisodesPerSecond = 0;
            ActionCounts = RlActionCounts.Empty;
            LatestInputs = null;
            LatestOutputs = null;
            LatestEpisodeBars = Array.Empty<HlocAvdtBar>();
            LatestEpisodeBarHistograms = Array.Empty<BarPriceHistogram>();
            LatestEpisodeWarmupBars = 0;
            LatestEpisodeSteppingMode = SimulationSteppingMode.Bar;
            LatestEpisodeCellWidth = 0;
            LatestEpisodeTrades = Array.Empty<Trade>();
            LatestEpisodeBarInputs = Array.Empty<IReadOnlyList<RlInputFeature>>();
            LatestEpisodeBarEquity = Array.Empty<double>();
            LatestEpisodeBarOutputs = Array.Empty<IReadOnlyList<RlOutputFeature>>();
            LatestEpisodeBarActions = Array.Empty<RlBarAction>();
            _episodeMetrics.Clear();
            _episodeTrades.Clear();
            _episodeWindows.Clear();
            _pauseAfterNextEpisode = pauseAfterFirstEpisode;
            _activeSnapshotId.Remove(brain.Name);
        }


        if (brain is IPolicyGradientBrain trainableBrain)
            trainableBrain.MoveToTrainingDevice();


        int warmupBars = brain is IPolicyGradientBrain warmupAwareBrain ? warmupAwareBrain.WarmupBars : 0;

        OnChanged?.Invoke();

        string source = historicRange is { } r ? r.Table : "dummy";
        string timing = timeConstraint.Enabled
            ? $"weekdays={string.Join(",", timeConstraint.AllowedWeekdays.OrderBy(d => d))}, " +
              $"session={EpisodeTimeConstraint.FormatClock(timeConstraint.SessionStart)}-{EpisodeTimeConstraint.FormatClock(timeConstraint.SessionEnd)}"
            : "unrestricted";
        string stepping = steppingMode == SimulationSteppingMode.Cell ? $"cell(width={cellWidth:0.#####})" : "bar";
        _logger.LogInformation(
            "Training started: brain={Brain}, strategy={Strategy}, rewardPolicy={RewardPolicy}, episodes={EpisodeCount}, episodeBars={EpisodeBars}, warmupBars={WarmupBars}, barWidthSeconds={BarWidthSeconds}, stepping={Stepping}, source={Source}, timing={Timing}",
            brainName, strategyName, rewardPolicy.Name, episodeCount, episodeBars, warmupBars, barWidthSeconds, stepping, source, timing);
        AppendLog("info",
            $"start: brain={brainName}, strategy={strategyName}, policy={rewardPolicy.Name}, {episodeCount} episodes x {episodeBars} bars @ {BarSizeOptions.Label(barWidthSeconds)}, step={stepping}, lr={learningRate:N4}, cash={startingCash:N0}, source={source}, timing={timing}");

        var barWidth = TimeSpan.FromSeconds(barWidthSeconds);

        var fetchSpan = barWidth * (episodeBars + warmupBars + EpisodeSpanBufferBars);

        var warmupSpan = barWidth * warmupBars;


        CancellationTokenSource? prefetchCts = null;
        Task? prefetchProducerTask = null;

        _ = Task.Run(() =>
        {
            try
            {
                if (historicRange is { } range)
                {
                    if (range.To - range.From < fetchSpan)
                        throw new InvalidOperationException(
                            $"selected range only spans {(range.To - range.From).TotalMinutes:N0} min, need at least " +
                            $"{fetchSpan.TotalMinutes:N0} min for a {episodeBars}-bar episode at {BarSizeOptions.Label(barWidthSeconds)} bars" +
                            (warmupBars > 0 ? $" (plus {warmupBars} warm-up bars)" : ""));

                    if (timeConstraint.Enabled)
                    {
                        if (timeConstraint.AllowedWeekdays.Count == 0)
                            throw new InvalidOperationException("episode timing: pick at least one weekday");
                        if (timeConstraint.SessionLength <= TimeSpan.Zero)
                            throw new InvalidOperationException("episode timing: session end must be after session start");
                        if (!EpisodeWindowSampler.EligibleDays(range.From, range.To, timeConstraint).Any())
                            throw new InvalidOperationException(
                                "episode timing: no day in the selected range has its full session window inside the range " +
                                "(narrow the weekday/session filters, or widen the Data Range)");
                    }
                }

                var optimizer = strategy.CreateOptimizer(brain, learningRate);
                var recentRewards = new Queue<double>();

                var recentEpisodeDurations = new Queue<double>();
                var episodeStopwatch = new Stopwatch();
                var rng = new Random();
                int syntheticTickCount = Math.Max(2, (int)(fetchSpan.TotalSeconds / SyntheticTickIntervalSeconds));

                var episodeQueue = Channel.CreateBounded<List<Tick>>(
                    new BoundedChannelOptions(PrefetchDepth) { SingleReader = true, SingleWriter = true });
                prefetchCts = new CancellationTokenSource();
                prefetchProducerTask = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i < episodeCount; i++)
                        {
                            var ticks = historicRange is { } episodeRange
                                ? RandomEpisodeWindow(episodeRange.Table, episodeRange.From, episodeRange.To, fetchSpan, warmupSpan, rng, timeConstraint).ToList()
                                : DummyTickGenerator.Generate(DateTime.UtcNow - fetchSpan, DateTime.UtcNow, syntheticTickCount).ToList();
                            await episodeQueue.Writer.WriteAsync(ticks, prefetchCts.Token);
                        }
                        episodeQueue.Writer.Complete();
                    }
                    catch (OperationCanceledException)
                    {
                        // Training loop stopped/errored before consuming everything it
                        // asked for -- nothing left to feed it, so just stop producing.
                    }
                    catch (Exception ex)
                    {
                        // Surfaces on the consumer's next ReadAsync below, same as if the
                        // fetch had failed synchronously in the old non-prefetching code.
                        episodeQueue.Writer.TryComplete(ex);
                    }
                });

                for (int episode = 0; episode < episodeCount; episode++)
                {
                    _resumeSignal.Wait();
                    if (_stopRequested)
                        break;
                    episodeStopwatch.Restart();

                    var episodeTicks = episodeQueue.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();

                    var result = RlEpisodeRunner.Run(
                        brain, episodeTicks, startingCash, episodeBars, barWidth, _traderFactory,
                        settings: traderSettings, warmupBars: warmupBars, rewardPolicy: rewardPolicy,
                        steppingMode: steppingMode, cellWidth: cellWidth);

                    double progress = episodeCount > 1 ? (double)episode / (episodeCount - 1) : 1.0;
                    double reward = strategy.TrainEpisode(brain, optimizer, result, progress);

                    recentRewards.Enqueue(reward);
                    if (recentRewards.Count > RecentRewardWindow)
                        recentRewards.Dequeue();

                    recentEpisodeDurations.Enqueue(episodeStopwatch.Elapsed.TotalSeconds);
                    if (recentEpisodeDurations.Count > RecentRewardWindow)
                        recentEpisodeDurations.Dequeue();

                    lock (_lock)
                    {
                        EpisodesCompleted = episode + 1;
                        _episodesTrainedByBrain[brainName] = episode + 1;
                        _lastTrainedAt[brainName] = DateTime.UtcNow;
                        RecentAverageReward = recentRewards.Average();
                        double durationSum = recentEpisodeDurations.Sum();
                        EpisodesPerSecond = durationSum > 0 ? recentEpisodeDurations.Count / durationSum : 0;
                        BestReward = EpisodesCompleted == 1 ? reward : Math.Max(BestReward, reward);
                        WorstReward = EpisodesCompleted == 1 ? reward : Math.Min(WorstReward, reward);
                        ActionCounts = result.Steps.Aggregate(ActionCounts, (counts, step) => counts.Add(step.Action));
                        if (result.LastInputs is not null)
                            LatestInputs = result.LastInputs;
                        if (result.LastOutputs is not null)
                            LatestOutputs = result.LastOutputs;
                        LatestEpisodeBars = result.Bars;
                        LatestEpisodeBarHistograms = result.BarHistograms;
                        LatestEpisodeWarmupBars = result.WarmupBars;
                        LatestEpisodeSteppingMode = result.SteppingMode;
                        LatestEpisodeCellWidth = result.CellWidth;
                        LatestEpisodeTrades = result.Trades;
                        LatestEpisodeBarInputs = result.BarInputs;
                        LatestEpisodeBarEquity = result.BarEquity;
                        LatestEpisodeBarOutputs = result.BarOutputs;
                        LatestEpisodeBarActions = result.BarActions;
                        _episodeMetrics.Add(new RlEpisodeMetric(episode + 1, reward, result.FinalCapital.Equity));
                        if (_episodeMetrics.Count > MaxMetricsLog)
                            _episodeMetrics.RemoveAt(0);
                        int blockedLongs = result.BarActions.Count(a => a.Action == SimulationAction.EnterLong && a.Blocked);
                        int blockedShorts = result.BarActions.Count(a => a.Action == SimulationAction.EnterShort && a.Blocked);
                        int blockedCloses = result.BarActions.Count(a => a.Action == SimulationAction.Close && a.Blocked);
                        _episodeTrades.Add(new RlEpisodeTrades(
                            episode + 1, result.Trades, result.MaxEquity, result.MaxDrawdown, blockedLongs, blockedShorts, blockedCloses));
                        if (_episodeTrades.Count > MaxMetricsLog)
                            _episodeTrades.RemoveAt(0);
                        if (historicRange is not null)
                        {
                            _episodeWindows.Add(new RlEpisodeWindow(episode + 1, result.WindowStart, result.WindowEnd));
                            if (_episodeWindows.Count > MaxMetricsLog)
                                _episodeWindows.RemoveAt(0);
                        }
                    }
                    if (episode == 0 || EpisodesCompleted % EpisodeLogInterval == 0)
                        AppendLog("info", $"episode {EpisodesCompleted}/{episodeCount}: reward={reward:N2}, equity={result.FinalCapital.Equity:N2}, avg={RecentAverageReward:N2}");
                    OnChanged?.Invoke();


                    if (_pauseAfterNextEpisode)
                    {
                        _pauseAfterNextEpisode = false;
                        lock (_lock)
                        {
                            if (IsRunning && !_stopRequested)
                            { IsPaused = true; _resumeSignal.Reset(); }
                        }
                        AppendLog("info", $"paused after episode {EpisodesCompleted} (step)");
                        OnChanged?.Invoke();
                    }
                }

                if (_stopRequested)
                {
                    _logger.LogInformation(
                        "Training stopped by user: brain={Brain}, episodesCompleted={EpisodesCompleted}",
                        brainName, EpisodesCompleted);
                    AppendLog("info", $"stopped by user after {EpisodesCompleted} episodes");
                }
                else
                {
                    _logger.LogInformation(
                        "Training completed: brain={Brain}, episodes={EpisodesCompleted}, bestReward={BestReward}, recentAvgReward={RecentAverageReward}",
                        brainName, EpisodesCompleted, BestReward, RecentAverageReward);
                    AppendLog("info", $"completed {EpisodesCompleted} episodes: best={BestReward:N2}, avg={RecentAverageReward:N2}");
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                { Error = ex.Message; }
                _logger.LogError(ex, "Training failed: brain={Brain}, strategy={Strategy}", brainName, strategyName);
                AppendLog("error", ex.Message);
            }
            finally
            {

                if (prefetchCts is not null)
                {
                    prefetchCts.Cancel();
                    try
                    { prefetchProducerTask?.GetAwaiter().GetResult(); }
                    catch { /* already surfaced above, or a clean cancellation -- either way nothing more to do */ }
                    prefetchCts.Dispose();
                }

                lock (_lock)
                { IsRunning = false; IsPaused = false; }
                OnChanged?.Invoke();
            }
        });

        return true;
    }

    public bool ResetRun()
    {
        lock (_lock)
        {
            if (IsRunning)
                return false;

            EpisodesCompleted = 0;
            TotalEpisodes = 0;
            RecentAverageReward = 0;
            BestReward = 0;
            WorstReward = 0;
            EpisodesPerSecond = 0;
            ActionCounts = RlActionCounts.Empty;
            LatestInputs = null;
            LatestOutputs = null;
            LatestEpisodeBars = Array.Empty<HlocAvdtBar>();
            LatestEpisodeBarHistograms = Array.Empty<BarPriceHistogram>();
            LatestEpisodeWarmupBars = 0;
            LatestEpisodeSteppingMode = SimulationSteppingMode.Bar;
            LatestEpisodeCellWidth = 0;
            LatestEpisodeTrades = Array.Empty<Trade>();
            LatestEpisodeBarInputs = Array.Empty<IReadOnlyList<RlInputFeature>>();
            LatestEpisodeBarEquity = Array.Empty<double>();
            LatestEpisodeBarOutputs = Array.Empty<IReadOnlyList<RlOutputFeature>>();
            LatestEpisodeBarActions = Array.Empty<RlBarAction>();
            _episodeMetrics.Clear();
            _episodeTrades.Clear();
            _episodeWindows.Clear();
            Error = null;
            AppendLog("info", "run reset");
        }
        OnChanged?.Invoke();
        return true;
    }


    public void PauseTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || IsPaused)
                return;
            IsPaused = true;
            _resumeSignal.Reset();
            AppendLog("info", "pause requested -- pausing after in-flight episode");
        }
        OnChanged?.Invoke();
    }

    public void ResumeTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || !IsPaused)
                return;
            IsPaused = false;
            _resumeSignal.Set();
            AppendLog("info", "resumed");
        }
        OnChanged?.Invoke();
    }

    public void StepTraining()
    {
        lock (_lock)
        {
            if (!IsRunning || !IsPaused)
                return;
            IsPaused = false;
            _pauseAfterNextEpisode = true;
            _resumeSignal.Set();
            AppendLog("info", "step: advancing one episode");
        }
        OnChanged?.Invoke();
    }

    public void StopTraining()
    {
        lock (_lock)
        {
            if (!IsRunning)
                return;
            _stopRequested = true;
            IsPaused = false;
            _resumeSignal.Set();
            AppendLog("info", "stop requested -- stopping after in-flight episode");
        }
        OnChanged?.Invoke();
    }


    public bool SaveBrain(
        string savedName, string brainName, string strategyName, string dataset,
        int episodeBars, int barWidthSeconds, double startingCash, double learningRate,
        Trader.TraderSettings? traderSettings = null)
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
            var parameters = new ModelTrainingParameters
            {
                EpisodeCount = _episodesTrainedByBrain.GetValueOrDefault(brainName),
                EpisodeBars = episodeBars,
                BarWidthSeconds = barWidthSeconds,
                StartingCash = startingCash,
                LearningRate = learningRate,
                LookbackBars = brain.WarmupBars > 0 ? brain.WarmupBars : null,
                Leverage = settings.Leverage,
                StopOutLevelPercent = settings.StopOutLevelPercent,
                CommissionRatePercent = settings.CommissionModel is Trader.Commission.PercentOfNotionalCommission p ? p.RatePercent : 0,
            };
            DateTime? trainedAt = _lastTrainedAt.TryGetValue(brainName, out var t) ? t : null;
            var recipe = _brainService.GetRecipe(brainName); // null for the 4 fixed Program.cs brains

            if (!_modelService.SaveModel(
                    savedName, brainName, strategyName, _rewardPolicyService.SelectedPolicyName, dataset, parameters, recipe, trainedAt,
                    brain.WeightCount(), RecentAverageReward, weights))
            {
                SaveStatus = $"save failed: {_modelService.Status}";
                AppendLog("error", $"save failed: {_modelService.Status}");
                OnChanged?.Invoke();
                _logger.LogWarning("Failed to save brain snapshot: name={SavedName}, brain={BrainName}, status={Status}", savedName, brainName, _modelService.Status);
                return false;
            }


            var savedId = _modelService.Models
                .Where(m => m.Name == savedName && m.BrainName == brainName)
                .OrderByDescending(m => m.SavedAt)
                .FirstOrDefault()?.Id;
            if (savedId is { } id)
                _activeSnapshotId[brainName] = id;
            SaveStatus = $"saved '{savedName}' at {DateTime.Now:HH:mm:ss}";
            AppendLog("info", $"saved snapshot '{savedName}' for {brainName}");
            OnChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"save failed: {ex.Message}";
            AppendLog("error", $"save failed: {ex.Message}");
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to save brain snapshot: name={SavedName}, brain={BrainName}", savedName, brainName);
            return false;
        }
    }


    public bool LoadSavedBrain(long id)
    {
        var summary = _modelService.Models.FirstOrDefault(m => m.Id == id);
        if (summary is null)
        {
            SaveStatus = "brain no longer exists";
            AppendLog("error", $"load failed: id {id} no longer exists");
            OnChanged?.Invoke();
            _logger.LogWarning("LoadSavedBrain: id {Id} no longer exists", id);
            return false;
        }

        var brain = _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == summary.BrainName);
        if (brain is null)
        {
            if (summary.Recipe is not { } recipe)
            {
                SaveStatus = $"no live '{summary.BrainName}' brain to load into, and no saved recipe to rebuild it from";
                OnChanged?.Invoke();
                _logger.LogWarning("LoadSavedBrain: no live '{BrainName}' brain and no recipe for id {Id}", summary.BrainName, id);
                return false;
            }
            try
            {
                brain = _brainService.BuildAndRegister(summary.BrainName, recipe);
                _logger.LogInformation("Rebuilt brain {BrainName} from saved recipe for id {Id}", summary.BrainName, id);
            }
            catch (Exception ex)
            {
                SaveStatus = $"couldn't rebuild '{summary.BrainName}' from its saved recipe: {ex.Message}";
                OnChanged?.Invoke();
                _logger.LogWarning(ex, "LoadSavedBrain: failed to rebuild '{BrainName}' from recipe for id {Id}", summary.BrainName, id);
                return false;
            }
        }

        try
        {
            var weights = _modelService.LoadWeights(id);
            if (weights is null)
            { SaveStatus = "weights not found"; AppendLog("error", $"load failed: weights not found for id {id}"); return false; }
            brain.ImportWeights(weights);

            _activeSnapshotId[summary.BrainName] = id;
            _episodesTrainedByBrain[summary.BrainName] = summary.Parameters.EpisodeCount;
            if (summary.TrainedAt is { } trainedAt)
                _lastTrainedAt[summary.BrainName] = trainedAt;
            SaveStatus = $"loaded '{summary.Name}' into '{summary.BrainName}' -- ready to use in Simulation";
            AppendLog("info", $"loaded '{summary.Name}' into {summary.BrainName}");
            _logger.LogInformation("Loaded saved brain {SavedName} (id={Id}) into live brain {BrainName}", summary.Name, id, summary.BrainName);
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"load failed: {ex.Message}";
            AppendLog("error", $"load failed: {ex.Message}");
            _logger.LogError(ex, "Failed to load saved brain id {Id}", id);
            return false;
        }
        finally
        {
            OnChanged?.Invoke();
        }
    }


    private IEnumerable<Tick> RandomEpisodeWindow(
        string table, DateTime from, DateTime to, TimeSpan fetchSpan, TimeSpan warmupSpan, Random rng, EpisodeTimeConstraint constraint) =>
        constraint.Enabled
            ? RandomConstrainedEpisodeWindow(table, from, to, fetchSpan, warmupSpan, rng, constraint)
            : RandomUnrestrictedEpisodeWindow(table, from, to, fetchSpan, rng);

    private IEnumerable<Tick> RandomUnrestrictedEpisodeWindow(string table, DateTime from, DateTime to, TimeSpan fetchSpan, Random rng)
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


    private IEnumerable<Tick> RandomConstrainedEpisodeWindow(
        string table, DateTime from, DateTime to, TimeSpan fetchSpan, TimeSpan warmupSpan, Random rng, EpisodeTimeConstraint constraint)
    {
        var eligibleDays = EpisodeWindowSampler.EligibleDays(from, to, constraint).ToList();
        if (eligibleDays.Count == 0)
        {
            _logger.LogWarning(
                "RandomConstrainedEpisodeWindow: no eligible day in {Table} for the configured weekday/session filters", table);
            return Array.Empty<Tick>();
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var day = eligibleDays[rng.Next(eligibleDays.Count)];
            var start = day + constraint.SessionStart - warmupSpan;
            if (_tickRepository.ReadTicks(table, start, start + fetchSpan).Any())
                return _tickRepository.ReadTicks(table, start, start + fetchSpan);
        }
        _logger.LogWarning(
            "RandomConstrainedEpisodeWindow: 5 random starts in {Table} landed in empty windows; falling back to the first eligible day's session",
            table);
        var fallbackStart = eligibleDays[0] + constraint.SessionStart - warmupSpan;
        return _tickRepository.ReadTicks(table, fallbackStart, fallbackStart + fetchSpan);
    }

}
