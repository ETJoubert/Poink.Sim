// ── Trained-model weight storage ─────────────────────────────────────────
//  Persists an IPolicyGradientBrain's exported weights (see that interface's
//  ExportWeights/ImportWeights) plus the strategy/parameters/metrics of the
//  run that produced them, so training progress survives a process restart
//  and past runs can be browsed/compared/deleted -- today MlpPolicyBrain is
//  a DI singleton and only keeps its weights in memory. Deliberately its own
//  Postgres database ("Poink.Sim", not "Poink.MarketData") since model
//  weights aren't market data and shouldn't share a db with the table the
//  cAlgo robot writes into (see ITickRepository's doc comment).
//
//  Every SaveModel call inserts a new row rather than upserting by brain
//  name -- each save is its own snapshot, so a brain's training history can
//  be browsed and individually deleted instead of only ever overwriting "the"
//  save for a given architecture.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Helpers;

namespace Poink.Sim.Repository;

// Free-form training-run parameters captured alongside a saved model's
// weights. Plain settable properties (not a record) so a JSON blob saved
// before a future field gets added here still deserializes cleanly -- the
// missing key just takes the C# default instead of failing. Public (not
// internal like most of this file) because it's exposed through
// TrainedModelSummary, which is itself public for the same reason (see that
// record's doc comment).
public sealed class ModelTrainingParameters
{
    public int EpisodeCount { get; set; }
    public int EpisodeBars { get; set; }

    // Seconds, not minutes despite older saved snapshots' JSON key having been named
    // "BarWidthMinutes" -- renamed when sub-minute bar sizes were added (see
    // BarSizeOptions). A row saved under the old key deserializes this to 0 (same
    // missing-key tolerance as every other field here), which ToString below shows as
    // "?" rather than a misleadingly precise-looking (but wrong-unit) value.
    public int BarWidthSeconds { get; set; }

    public double StartingCash { get; set; }
    public double LearningRate { get; set; }

    // How far back (in bars) this brain's windowed encoder(s) looked --
    // null for a brain with no windowed encoder (e.g. a bare
    // TraderStateEncoder brain) and for any snapshot saved before this field
    // existed (see this class's own doc comment on why that deserializes
    // cleanly).
    public int? LookbackBars { get; set; }

    // CFD account settings the run trained under -- default to pre-CFD
    // behavior (1x, no stop-out risk, no commission) for any snapshot saved
    // before these fields existed, same deserialization-tolerance reasoning
    // as the rest of this class.
    public double Leverage { get; set; } = 1.0;
    public double StopOutLevelPercent { get; set; } = 50.0;
    public double CommissionRatePercent { get; set; }

    // Single shared display format -- InvariantCulture because this host's
    // locale is comma-decimal (see TrainPage.razor.cs's Pct helper for the
    // same concern) -- "cash=1,000" must never render as "cash=1 000".
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture,
            $"episodes={EpisodeCount} bars={EpisodeBars}@{(BarWidthSeconds > 0 ? BarSizeOptions.Label(BarWidthSeconds) : "?")} cash={StartingCash:N0} lr={LearningRate:G3}" +
            $" lev={Leverage:G3}x commission={CommissionRatePercent:G3}%{(LookbackBars is { } lookback ? $" lookback={lookback}" : "")}");
}

// Everything about a saved snapshot except the weights themselves, which are
// loaded separately (LoadWeights) -- a listing screen has no reason to pull
// megabytes of bytea per row just to show a table of metadata. Dataset is a
// plain human-readable reference (e.g. a tick table + date range, or
// "synthetic") -- nothing here re-reads or re-validates it against the actual
// tick data, it's purely informational. Recipe is null for anything not
// built from a saved BrainRecipe. TrainedAt is null for a snapshot saved
// before any training episode actually completed for that brain -- distinct
// from SavedAt, which is just when this row was inserted; a brain can be
// saved untrained.
public sealed record TrainedModelSummary(
    long Id, string Name, string BrainName, string StrategyName, string RewardPolicyName, string Dataset,
    ModelTrainingParameters Parameters, BrainRecipe? Recipe, DateTime? TrainedAt,
    long WeightCount, double AvgReward, DateTime SavedAt);

internal interface IModelRepository
{
    long SaveModel(
        string name, string brainName, string strategyName, string rewardPolicyName, string dataset,
        ModelTrainingParameters parameters, BrainRecipe? recipe, DateTime? trainedAt,
        long weightCount, double avgReward, byte[] weights);

    List<TrainedModelSummary> ListModels();

    byte[]? LoadWeights(long id);

    void DeleteModel(long id);
}
