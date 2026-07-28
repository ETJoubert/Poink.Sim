// ── Trained-brain weight storage ─────────────────────────────────────────
//  Persists an IPolicyGradientBrain's exported weights (see that interface's
//  ExportWeights/ImportWeights) plus the strategy/parameters/metrics of the
//  run that produced them, so training progress survives a process restart
//  and past runs can be browsed/compared/deleted -- today MlpPolicyBrain is
//  a DI singleton and only keeps its weights in memory. Deliberately its own
//  Postgres database ("Poink.Sim", not "Poink.MarketData") since model
//  weights aren't market data and shouldn't share a db with the table the
//  cAlgo robot writes into (see ITickRepository's doc comment).
//
//  Every SaveBrain call inserts a new row rather than upserting by brain
//  name -- each save is its own snapshot, so a brain's training history can
//  be browsed and individually deleted instead of only ever overwriting "the"
//  save for a given architecture.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Repository;

// Free-form training-run parameters captured alongside a saved brain's
// weights. Plain settable properties (not a record) so a JSON blob saved
// before a future field gets added here still deserializes cleanly -- the
// missing key just takes the C# default instead of failing. Public (not
// internal like most of this file) because it's exposed through
// SavedBrainSummary, which is itself public for the same reason (see that
// record's doc comment).
public sealed class BrainTrainingParameters
{
    public int EpisodeCount { get; set; }
    public int EpisodeBars { get; set; }
    public int BarWidthMinutes { get; set; }
    public double StartingCash { get; set; }
    public double LearningRate { get; set; }

    // How far back (in bars) this brain's windowed encoder(s) looked --
    // populated from the brain's own WarmupBars at save time (see
    // RlTrainingService.SaveBrain), null for a brain with no windowed
    // encoder (e.g. a bare TraderStateEncoder brain) and for any snapshot
    // saved before this field existed (see this class's own doc comment on
    // why that deserializes cleanly).
    public int? LookbackBars { get; set; }

    // CFD account settings the run trained under -- default to pre-CFD
    // behavior (1x, no stop-out risk, no commission) for any snapshot saved
    // before these fields existed, same deserialization-tolerance reasoning
    // as the rest of this class.
    public double Leverage { get; set; } = 1.0;
    public double StopOutLevelPercent { get; set; } = 50.0;
    public double CommissionRatePercent { get; set; }

    // Single shared display format -- both the Train page's Saved Brains table
    // and SimulationPage's Brain info card show this, and duplicating the
    // format string between them would drift. InvariantCulture because this
    // host's locale is comma-decimal (see TrainPage.razor.cs's Pct helper for
    // the same concern) -- "cash=1,000" must never render as "cash=1 000".
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture,
            $"episodes={EpisodeCount} bars={EpisodeBars}@{BarWidthMinutes}m cash={StartingCash:N0} lr={LearningRate:G3}" +
            $" lev={Leverage:G3}x commission={CommissionRatePercent:G3}%{(LookbackBars is { } lookback ? $" lookback={lookback}" : "")}");
}

// Everything about a saved snapshot except the weights themselves, which are
// loaded separately (LoadWeights) -- a listing screen has no reason to pull
// megabytes of bytea per row just to show a table of metadata. Dataset is a
// plain human-readable reference (e.g. a tick table + date range, or
// "synthetic") -- nothing here re-reads or re-validates it against the actual
// tick data, it's purely informational. Public (not internal like everything
// else in this file) because it's passed as a [Parameter] into BrainInfoCard
// (see RlEpisodeMetric's doc comment for why a Razor component parameter
// can't be typed with a less-accessible type).
// Recipe is null for anything not built through RlTrainingService.TryBuildBrain
// -- the four Program.cs-fixed brains reconstruct via their own source code
// every restart instead (see BrainRecipe's doc comment). TrainedAt is null for
// a snapshot saved before any training episode actually completed for that
// brain (see RlTrainingService's _lastTrainedAt) -- distinct from SavedAt,
// which is just when this row was inserted; a brain can be saved untrained.
public sealed record SavedBrainSummary(
    long Id, string Name, string BrainName, string StrategyName, string RewardPolicyName, string Dataset,
    BrainTrainingParameters Parameters, BrainRecipe? Recipe, DateTime? TrainedAt,
    long WeightCount, double AvgReward, DateTime SavedAt);

internal interface IBrainRepository
{
    long SaveBrain(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        BrainTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights);

    List<SavedBrainSummary> ListBrains();

    byte[]? LoadWeights(long id);

    void DeleteBrain(long id);
}
