using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Poink.Sim.Components.Pages.Code.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages.Code.Services;

internal sealed class CodeGraphProvider : ICodeGraphProvider
{
    private const string TrainingSettings = "training settings";
    private const string LearnRate = "learn rate";
    private const string EpisodeRequest = "episode request";
    private const string TickQuery = "table, from, to";
    private const string TickStream = "IEnumerable<Tick>";
    private const string TickWindow = "List<Tick>";
    private const string Tick = "Tick";
    private const string TimeConstraint = "EpisodeTimeConstraint";
    private const string ReplayConfig = "SimulationConfig";
    private const string Snapshot = "SimulationSnapshot";
    private const string Bar = "HlocAvdtBar";
    private const string Histogram = "BarPriceHistogram";
    private const string AccountState = "CapitalState, Position?";
    private const string TradeExecution = "TradeExecution?";
    private const string TraderConfig = "TraderSettings";
    private const string Observation = "SimulationObservation";
    private const string CurrentPrice = "current price";
    private const string RawFeature = "raw feature value";
    private const string NormalizedFeature = "normalized float";
    private const string EncoderSizes = "EncoderSizeOptions";
    private const string FeatureVector = "float[]";
    private const string FeatureTensor = "Tensor [1, F]";
    private const string Recipe = "BrainRecipe";
    private const string Architecture = "architecture, hidden sizes";
    private const string DirectionLogits = "direction logits";
    private const string HeadLogits = "head logit slices";
    private const string ActionIndex = "action index";
    private const string LogProbability = "log probability";
    private const string Entropy = "entropy";
    private const string HeadValues = "head values";
    private const string Decision = "SimulationDecision";
    private const string RewardValue = "double reward";
    private const string Episode = "RlEpisode";
    private const string GradientStep = "gradient step";

    private const string SetOnTrainPage = "set on /train";

    private readonly RlTrainingService _training;
    private readonly SimulationEngine _simulation;

    public CodeGraphProvider(RlTrainingService training, SimulationEngine simulation)
    {
        _training = training;
        _simulation = simulation;
    }

    public CodeGraph Build() => new(BuildNodes(), BuildEdges());

    private IReadOnlyList<CodeNode> BuildNodes() =>
    [
        new CodeNode(
            "run-config",
            "Training Run",
            CodeConcept.Config,
            Column: 0,
            Row: 0,
            "What you set before a run starts on the Train page. Chooses the brain, strategy and reward policy, and sizes every episode in the run.",
            [
                new CodeTypeRef("RlTrainingService.TryStartTraining", "Services/Training/RlTrainingService.cs"),
                new CodeTypeRef("ModelTrainingParameters", "Repository/IModelRepository.cs")
            ],
            [],
            [new CodePort(TrainingSettings), new CodePort(LearnRate)],
            [],
            RunSettings()),

        new CodeNode(
            "training-service",
            "Training Service",
            CodeConcept.Simulation,
            Column: 1,
            Row: 0,
            "Owns the training run: requests episode windows, prefetches ticks through a bounded channel, drives the episode runner and hands each finished episode to the training strategy.",
            [
                new CodeTypeRef("RlTrainingService", "Services/Training/RlTrainingService.cs"),
                new CodeTypeRef("RlEpisodeMetric", "Services/Training/RlEpisode.cs"),
                new CodeTypeRef("BrainRecipe", "Brain/Neural/BrainCatalog.cs")
            ],
            [new CodePort(TrainingSettings)],
            [new CodePort(EpisodeRequest)],
            TrainingBadges(),
            []),

        new CodeNode(
            "sim-engine",
            "Sim Engine",
            CodeConcept.Simulation,
            Column: 2,
            Row: 0,
            "Interactive replay driver. Play, pause, step and speed control over the same aggregator, brain and trader the training loop uses.",
            [
                new CodeTypeRef("SimulationEngine", "Services/Simulation/SimulationEngine.cs"),
                new CodeTypeRef("SimulationSnapshot", "Models/SimulationSnapshot.cs"),
                new CodeTypeRef("RingBuffer", "Services/Simulation/RingBuffer.cs")
            ],
            [new CodePort(ReplayConfig), new CodePort(TickStream)],
            [new CodePort(Tick), new CodePort(Snapshot)],
            SimulationBadges(),
            []),

        new CodeNode(
            "episode-runner",
            "Episode Runner",
            CodeConcept.Simulation,
            Column: 3,
            Row: 0,
            "Headless tick loop for one episode. Advances the trader, aggregates bars, skips warmup, records a step per completed bar and returns the assembled episode.",
            [
                new CodeTypeRef("RlEpisodeRunner", "Services/Training/RlEpisodeRunner.cs"),
                new CodeTypeRef("RlEpisode", "Services/Training/RlEpisode.cs"),
                new CodeTypeRef("RlStep", "Services/Training/RlEpisode.cs"),
                new CodeTypeRef("RlBarAction", "Services/Training/RlEpisode.cs")
            ],
            [new CodePort(TickWindow), new CodePort(EpisodeRequest)],
            [new CodePort(Tick), new CodePort(Episode)],
            EpisodeRunnerBadges(),
            []),

        new CodeNode(
            "data-config",
            "Tick Range",
            CodeConcept.Config,
            Column: 0,
            Row: 1,
            "The historic slice the run reads from. Names the Postgres tick table and the outer date range every episode window is drawn from.",
            [
                new CodeTypeRef("ITickRepository.ReadTicks", "Repository/ITickRepository.cs"),
                new CodeTypeRef("TickDataSource", "Components/Shared/TickDataSource/")
            ],
            [],
            [new CodePort(TickQuery)],
            [],
            DataSettings()),

        new CodeNode(
            "ticks",
            "Tick Source",
            CodeConcept.Data,
            Column: 1,
            Row: 1,
            "Raw market data. Reads bid and ask ticks from the Postgres tick tables, or synthesises them when no database is attached.",
            [
                new CodeTypeRef("ITickRepository", "Repository/ITickRepository.cs"),
                new CodeTypeRef("TickRepository", "Repository/TickRepository.cs"),
                new CodeTypeRef("DummyTickGenerator", "Services/Simulation/DummyTickGenerator.cs"),
                new CodeTypeRef("Tick", "Repository/ITickRepository.cs")
            ],
            [new CodePort(TickQuery)],
            [new CodePort(TickStream)],
            [],
            []),

        new CodeNode(
            "sampler",
            "Episode Sampler",
            CodeConcept.Data,
            Column: 2,
            Row: 1,
            "Picks the random time window for the next episode. Honours the configured time-of-day and weekday constraints and sizes the window from bar width, episode bars and warmup bars.",
            [
                new CodeTypeRef("EpisodeWindowSampler", "Services/Training/EpisodeWindowSampler.cs"),
                new CodeTypeRef("EpisodeTimeConstraint", "Services/Training/EpisodeTimeConstraint.cs"),
                new CodeTypeRef("RlEpisodeWindow", "Services/Training/RlEpisode.cs")
            ],
            [new CodePort(TickStream), new CodePort(EpisodeRequest), new CodePort(TimeConstraint)],
            [new CodePort(TickWindow)],
            SamplerBadges(),
            []),

        new CodeNode(
            "aggregator",
            "Bar Aggregator",
            CodeConcept.Data,
            Column: 3,
            Row: 1,
            "Folds ticks into fixed-width bars and builds the per-bar price histogram that the volume and time profile encoders read.",
            [
                new CodeTypeRef("SimulationBarAggregator", "Services/Simulation/SimulationBarAggregator.cs"),
                new CodeTypeRef("HlocAvdtBar", "Models/HlocAvdtBar.cs"),
                new CodeTypeRef("BarPriceHistogram", "Models/BarPriceHistogram.cs")
            ],
            [new CodePort(Tick)],
            [new CodePort(Bar), new CodePort(Histogram)],
            [],
            []),

        new CodeNode(
            "observation",
            "Observation",
            CodeConcept.Inputs,
            Column: 4,
            Row: 1,
            "The complete view the brain gets for one decision: recent bars, recent ticks, bar histograms, the live capital state, the open position and the trade log.",
            [
                new CodeTypeRef("SimulationObservation", "Models/SimulationDecision.cs"),
                new CodeTypeRef("CapitalState", "Models/SimulationAccount.cs"),
                new CodeTypeRef("Position", "Models/SimulationAccount.cs")
            ],
            [new CodePort(Bar), new CodePort(Histogram), new CodePort(AccountState)],
            [new CodePort(Observation), new CodePort(CurrentPrice)],
            [],
            []),

        new CodeNode(
            "replay-config",
            "Replay Config",
            CodeConcept.Config,
            Column: 1,
            Row: 2,
            "What the Simulation page loads a replay with. Table, range, bar size, which brain observes the stream, and the account each trader starts from.",
            [
                new CodeTypeRef("SimulationConfig", "Models/SimulationSnapshot.cs")
            ],
            [],
            [new CodePort(ReplayConfig)],
            [],
            ReplaySettings()),

        new CodeNode(
            "episode-config",
            "Episode Window",
            CodeConcept.Config,
            Column: 2,
            Row: 2,
            "Restricts which slices of history an episode may be drawn from, so the brain trains on the sessions and weekdays you care about.",
            [
                new CodeTypeRef("EpisodeTimeConstraint", "Services/Training/EpisodeTimeConstraint.cs")
            ],
            [],
            [new CodePort(TimeConstraint)],
            [],
            EpisodeSettings()),

        new CodeNode(
            "normalizers",
            "Normalizers",
            CodeConcept.Inputs,
            Column: 4,
            Row: 2,
            "Per-feature scaling applied inside the encoders so raw prices, spreads, equity ratios and flags all reach the network on comparable scales.",
            [
                new CodeTypeRef("IFeatureNormalizer", "Brain/Neural/Normalizers/IFeatureNormalizer.cs"),
                new CodeTypeRef("ZScoreNormalizer", "Brain/Neural/Normalizers/"),
                new CodeTypeRef("PriceOffsetNormalizer", "Brain/Neural/Normalizers/"),
                new CodeTypeRef("EquityRatioNormalizer", "Brain/Neural/Normalizers/"),
                new CodeTypeRef("BinaryNormalizer", "Brain/Neural/Normalizers/")
            ],
            [new CodePort(RawFeature)],
            [new CodePort(NormalizedFeature)],
            [new CodeBadge("impls", "9")],
            []),

        new CodeNode(
            "encoders",
            "Encoders",
            CodeConcept.Inputs,
            Column: 5,
            Row: 1,
            "Transform the observation into the flat feature vector. The composite encoder concatenates the enabled parts and keeps any sequence-bearing encoder first so recurrent architectures can slice the leading window.",
            [
                new CodeTypeRef("IObservationEncoder", "Brain/Neural/Encoders/IObservationEncoder.cs"),
                new CodeTypeRef("CompositeEncoder", "Brain/Neural/Encoders/CompositeEncoder.cs"),
                new CodeTypeRef("BarWindowEncoder", "Brain/Neural/Encoders/BarWindowEncoder.cs"),
                new CodeTypeRef("VolumeProfileEncoder", "Brain/Neural/Encoders/VolumeProfileEncoder.cs"),
                new CodeTypeRef("TimeProfileEncoder", "Brain/Neural/Encoders/TimeProfileEncoder.cs"),
                new CodeTypeRef("SessionClockEncoder", "Brain/Neural/Encoders/SessionClockEncoder.cs"),
                new CodeTypeRef("SpreadVolatilityEncoder", "Brain/Neural/Encoders/SpreadVolatilityEncoder.cs"),
                new CodeTypeRef("TraderStateEncoder", "Brain/Neural/Encoders/TraderStateEncoder.cs")
            ],
            [new CodePort(Observation), new CodePort(NormalizedFeature), new CodePort(EncoderSizes)],
            [new CodePort(FeatureVector)],
            EncoderBadges(),
            []),

        new CodeNode(
            "brain",
            "Brain",
            CodeConcept.Network,
            Column: 6,
            Row: 1,
            "The composed policy: encoders, an architecture and action heads bound together. Encodes the observation to a tensor, runs the network and decodes the result into a decision with its log probability.",
            [
                new CodeTypeRef("ISimulationBrain", "Brain/ISimulationBrain.cs"),
                new CodeTypeRef("IPolicyGradientBrain", "Brain/Neural/IPolicyGradientBrain.cs"),
                new CodeTypeRef("ComposedPolicyBrain", "Brain/Neural/ComposedPolicyBrain.cs"),
                new CodeTypeRef("BrainCatalog", "Brain/Neural/BrainCatalog.cs"),
                new CodeTypeRef("BrainRegistry", "Brain/BrainRegistry.cs")
            ],
            [new CodePort(FeatureVector), new CodePort(Recipe)],
            [new CodePort(FeatureTensor)],
            BrainBadges(),
            []),

        new CodeNode(
            "brain-config",
            "Brain Recipe",
            CodeConcept.Config,
            Column: 6,
            Row: 2,
            "The blueprint the brain is built from. Which encoders are enabled and how wide each one is, which architecture backs them, and how the decision is collapsed.",
            [
                new CodeTypeRef("BrainRecipe", "Brain/Neural/BrainCatalog.cs"),
                new CodeTypeRef("EncoderSizeOptions", "Brain/Neural/BrainCatalog.cs"),
                new CodeTypeRef("BrainBuilder", "Components/Pages/Training/Components/BrainBuilder/")
            ],
            [],
            [new CodePort(Recipe), new CodePort(Architecture), new CodePort(EncoderSizes)],
            [],
            BrainSettings()),

        new CodeNode(
            "network",
            "Network",
            CodeConcept.Network,
            Column: 7,
            Row: 1,
            "The trainable TorchSharp module built from the chosen architecture. Emits one flat logit vector covering the enabled directions followed by each action head's slice.",
            [
                new CodeTypeRef("INetworkArchitecture", "Brain/Neural/Architectures/INetworkArchitecture.cs"),
                new CodeTypeRef("MlpArchitecture", "Brain/Neural/Architectures/MlpArchitecture.cs"),
                new CodeTypeRef("LstmArchitecture", "Brain/Neural/Architectures/LstmArchitecture.cs"),
                new CodeTypeRef("CnnArchitecture", "Brain/Neural/Architectures/CnnArchitecture.cs"),
                new CodeTypeRef("NetworkLayerShape", "Brain/Neural/IPolicyGradientBrain.cs")
            ],
            [new CodePort(FeatureTensor), new CodePort(Architecture), new CodePort(GradientStep)],
            [new CodePort(DirectionLogits), new CodePort(HeadLogits)],
            [new CodeBadge("architectures", "3")],
            []),

        new CodeNode(
            "decoder",
            "Decoder",
            CodeConcept.Outputs,
            Column: 8,
            Row: 1,
            "Collapses the direction slice of the logits to one action. Softmax sampling or hard argmax, temperature scaled, returning the log probability and entropy the policy gradient needs.",
            [
                new CodeTypeRef("CategoricalSampling", "Brain/Neural/CategoricalSampling.cs"),
                new CodeTypeRef("DecisionDecodingKind", "Brain/Neural/BrainCatalog.cs"),
                new CodeTypeRef("RlOutputFeature", "Brain/Neural/IPolicyGradientBrain.cs")
            ],
            [new CodePort(DirectionLogits)],
            [new CodePort(ActionIndex), new CodePort(LogProbability), new CodePort(Entropy)],
            DecoderBadges(),
            []),

        new CodeNode(
            "heads",
            "Action Heads",
            CodeConcept.Outputs,
            Column: 8,
            Row: 2,
            "Each head owns a contiguous logit slice and samples a discrete level from it. Volume fraction, stop loss distance and take profit distance, expressed as fractions of the current price.",
            [
                new CodeTypeRef("IActionOutputHead", "Brain/Neural/ActionOutputs/IActionOutputHead.cs"),
                new CodeTypeRef("DiscretizedLevelsActionHead", "Brain/Neural/ActionOutputs/DiscretizedLevelsActionHead.cs"),
                new CodeTypeRef("ActionOutputKind", "Brain/Neural/BrainCatalog.cs")
            ],
            [new CodePort(HeadLogits)],
            [new CodePort(HeadValues), new CodePort(LogProbability)],
            [new CodeBadge("kinds", "Volume, Stop, Target")],
            []),

        new CodeNode(
            "decision",
            "Decision",
            CodeConcept.Actions,
            Column: 9,
            Row: 1,
            "The concrete instruction leaving the brain: hold, enter long, enter short or close, together with the capital fraction and any stop loss and take profit prices.",
            [
                new CodeTypeRef("SimulationDecision", "Models/SimulationDecision.cs"),
                new CodeTypeRef("SimulationAction", "Models/SimulationDecision.cs"),
                new CodeTypeRef("OrderType", "Models/SimulationDecision.cs")
            ],
            [new CodePort(ActionIndex), new CodePort(HeadValues), new CodePort(CurrentPrice)],
            [new CodePort(Decision)],
            DecisionBadges(),
            []),

        new CodeNode(
            "strategy",
            "Training Strategy",
            CodeConcept.Rewards,
            Column: 9,
            Row: 2,
            "Turns a finished episode into a weight update. Discounts and normalizes returns, builds the REINFORCE loss with an annealed entropy bonus and steps the optimizer.",
            [
                new CodeTypeRef("IRlTrainingStrategy", "Services/Training/IRlTrainingStrategy.cs"),
                new CodeTypeRef("PolicyGradientTrainingStrategy", "Services/Training/PolicyGradientTrainingStrategy.cs")
            ],
            [
                new CodePort(Episode), new CodePort(RewardValue), new CodePort(LogProbability),
                new CodePort(Entropy), new CodePort(LearnRate)
            ],
            [new CodePort(GradientStep)],
            StrategyBadges(),
            []),

        new CodeNode(
            "trader",
            "Trader",
            CodeConcept.Trader,
            Column: 10,
            Row: 1,
            "Executes decisions against the account. Advances stop loss, take profit, margin stop-out and pending orders on every tick, sizes positions from capital fraction and leverage and charges commission on open and close.",
            [
                new CodeTypeRef("ITrader", "Trader/ITrader.cs"),
                new CodeTypeRef("Trader", "Trader/Trader.cs"),
                new CodeTypeRef("ITraderFactory", "Trader/TraderFactory.cs"),
                new CodeTypeRef("TraderSettings", "Trader/TraderSettings.cs"),
                new CodeTypeRef("ICommissionModel", "Trader/Commission/ICommissionModel.cs"),
                new CodeTypeRef("PendingOrder", "Trader/Orders/PendingOrder.cs")
            ],
            [new CodePort(Decision), new CodePort(TraderConfig)],
            [new CodePort(AccountState), new CodePort(TradeExecution)],
            [],
            []),

        new CodeNode(
            "trader-config",
            "Trader Settings",
            CodeConcept.Config,
            Column: 10,
            Row: 2,
            "The account rules every trader is created with. Leverage multiplies the capital fraction into notional, the stop-out level force-closes on margin, and the commission model is charged on open and close.",
            [
                new CodeTypeRef("TraderSettings", "Trader/TraderSettings.cs"),
                new CodeTypeRef("ICommissionModel", "Trader/Commission/ICommissionModel.cs"),
                new CodeTypeRef("PercentOfNotionalCommission", "Trader/Commission/PercentOfNotionalCommission.cs")
            ],
            [],
            [new CodePort(TraderConfig)],
            [],
            TraderSettingsValues()),

        new CodeNode(
            "reward",
            "Reward Policy",
            CodeConcept.Rewards,
            Column: 11,
            Row: 1,
            "Scores the outcome of the previous step from the capital and position change. The chosen policy decides whether the brain is paid for equity drift, risk-adjusted return, rule compliance or market structure.",
            [
                new CodeTypeRef("IRewardPolicy", "Services/Training/RewardPolicies/IRewardPolicy.cs"),
                new CodeTypeRef("RewardContext", "Services/Training/RewardPolicies/IRewardPolicy.cs"),
                new CodeTypeRef("EquityDriftRewardPolicy", "Services/Training/RewardPolicies/"),
                new CodeTypeRef("DifferentialSharpeRewardPolicy", "Services/Training/RewardPolicies/"),
                new CodeTypeRef("TradingRulesRewardPolicy", "Services/Training/RewardPolicies/"),
                new CodeTypeRef("MarketStructureRewardPolicy", "Services/Training/RewardPolicies/")
            ],
            [new CodePort(AccountState)],
            [new CodePort(RewardValue)],
            RewardBadges(),
            [])
    ];

    private static IReadOnlyList<CodeEdge> BuildEdges() =>
    [
        new CodeEdge("ticks", TickStream, "sampler", TickStream, CodeEdgeKind.Flow),
        new CodeEdge("ticks", TickStream, "sim-engine", TickStream, CodeEdgeKind.Flow),
        new CodeEdge("sampler", TickWindow, "episode-runner", TickWindow, CodeEdgeKind.Flow),
        new CodeEdge("episode-runner", Tick, "aggregator", Tick, CodeEdgeKind.Flow),
        new CodeEdge("aggregator", Bar, "observation", Bar, CodeEdgeKind.Flow),
        new CodeEdge("aggregator", Histogram, "observation", Histogram, CodeEdgeKind.Flow),
        new CodeEdge("trader", AccountState, "observation", AccountState, CodeEdgeKind.Flow),
        new CodeEdge("observation", Observation, "encoders", Observation, CodeEdgeKind.Flow),
        new CodeEdge("normalizers", NormalizedFeature, "encoders", NormalizedFeature, CodeEdgeKind.Flow),
        new CodeEdge("encoders", FeatureVector, "brain", FeatureVector, CodeEdgeKind.Flow),
        new CodeEdge("brain", FeatureTensor, "network", FeatureTensor, CodeEdgeKind.Flow),
        new CodeEdge("network", DirectionLogits, "decoder", DirectionLogits, CodeEdgeKind.Flow),
        new CodeEdge("network", HeadLogits, "heads", HeadLogits, CodeEdgeKind.Flow),
        new CodeEdge("decoder", ActionIndex, "decision", ActionIndex, CodeEdgeKind.Flow),
        new CodeEdge("heads", HeadValues, "decision", HeadValues, CodeEdgeKind.Flow),
        new CodeEdge("observation", CurrentPrice, "decision", CurrentPrice, CodeEdgeKind.Flow),
        new CodeEdge("decision", Decision, "trader", Decision, CodeEdgeKind.Flow),
        new CodeEdge("trader", AccountState, "reward", AccountState, CodeEdgeKind.Flow),
        new CodeEdge("reward", RewardValue, "strategy", RewardValue, CodeEdgeKind.Flow),
        new CodeEdge("decoder", LogProbability, "strategy", LogProbability, CodeEdgeKind.Flow),
        new CodeEdge("decoder", Entropy, "strategy", Entropy, CodeEdgeKind.Flow),
        new CodeEdge("heads", LogProbability, "strategy", LogProbability, CodeEdgeKind.Flow),
        new CodeEdge("episode-runner", Episode, "strategy", Episode, CodeEdgeKind.Flow),

        new CodeEdge("strategy", GradientStep, "network", GradientStep, CodeEdgeKind.Feedback),

        new CodeEdge("training-service", EpisodeRequest, "sampler", EpisodeRequest, CodeEdgeKind.Drives),
        new CodeEdge("training-service", EpisodeRequest, "episode-runner", EpisodeRequest, CodeEdgeKind.Drives),
        new CodeEdge("sim-engine", Tick, "aggregator", Tick, CodeEdgeKind.Drives),

        new CodeEdge("run-config", TrainingSettings, "training-service", TrainingSettings, CodeEdgeKind.Config),
        new CodeEdge("run-config", LearnRate, "strategy", LearnRate, CodeEdgeKind.Config),
        new CodeEdge("data-config", TickQuery, "ticks", TickQuery, CodeEdgeKind.Config),
        new CodeEdge("replay-config", ReplayConfig, "sim-engine", ReplayConfig, CodeEdgeKind.Config),
        new CodeEdge("episode-config", TimeConstraint, "sampler", TimeConstraint, CodeEdgeKind.Config),
        new CodeEdge("brain-config", Recipe, "brain", Recipe, CodeEdgeKind.Config),
        new CodeEdge("brain-config", Architecture, "network", Architecture, CodeEdgeKind.Config),
        new CodeEdge("brain-config", EncoderSizes, "encoders", EncoderSizes, CodeEdgeKind.Config),
        new CodeEdge("trader-config", TraderConfig, "trader", TraderConfig, CodeEdgeKind.Config)
    ];

    private IReadOnlyList<CodeSetting> RunSettings()
    {
        var episodes = _training.TotalEpisodes > 0
            ? new CodeSetting("Episodes", _training.TotalEpisodes.ToString("N0", CultureInfo.InvariantCulture), IsLive: true)
            : new CodeSetting("Episodes", SetOnTrainPage);

        return
        [
            episodes,
            new CodeSetting("Strategy", _training.StrategyNames.FirstOrDefault() ?? SetOnTrainPage),
            new CodeSetting("Reward policy", _training.RewardPolicyNames.FirstOrDefault() ?? SetOnTrainPage),
            new CodeSetting("Bars / episode", SetOnTrainPage),
            new CodeSetting("Learn rate", SetOnTrainPage)
        ];
    }

    private IReadOnlyList<CodeSetting> DataSettings()
    {
        var config = _simulation.Snapshot?.Config;
        return config is null
            ?
            [
                new CodeSetting("Table", SetOnTrainPage),
                new CodeSetting("From", SetOnTrainPage),
                new CodeSetting("To", SetOnTrainPage)
            ]
            :
            [
                new CodeSetting("Table", config.Table, IsLive: true),
                new CodeSetting("From", config.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), IsLive: true),
                new CodeSetting("To", config.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), IsLive: true)
            ];
    }

    private IReadOnlyList<CodeSetting> ReplaySettings()
    {
        var config = _simulation.Snapshot?.Config;
        return config is null
            ?
            [
                new CodeSetting("Table", "not loaded"),
                new CodeSetting("Bar size", "not loaded"),
                new CodeSetting("Brain", "not loaded"),
                new CodeSetting("Starting cash", "not loaded"),
                new CodeSetting("Traders", "not loaded")
            ]
            :
            [
                new CodeSetting("Table", config.Table, IsLive: true),
                new CodeSetting("Bar size", config.Aggregate ? FormatSpan(config.BarSize) : "raw ticks", IsLive: true),
                new CodeSetting("Brain", config.BrainName, IsLive: true),
                new CodeSetting("Starting cash", config.StartingCash.ToString("N0", CultureInfo.InvariantCulture), IsLive: true),
                new CodeSetting("Traders", config.TraderCount.ToString(CultureInfo.InvariantCulture), IsLive: true)
            ];
    }

    private static IReadOnlyList<CodeSetting> EpisodeSettings()
    {
        var none = EpisodeTimeConstraint.None;
        return
        [
            new CodeSetting("Enabled", none.Enabled ? "yes" : "no"),
            new CodeSetting("Weekdays", "all 7"),
            new CodeSetting("Session start", EpisodeTimeConstraint.FormatClock(none.SessionStart)),
            new CodeSetting("Session end", EpisodeTimeConstraint.FormatClock(none.SessionEnd))
        ];
    }

    private static IReadOnlyList<CodeSetting> BrainSettings() =>
    [
        new CodeSetting("Bar window", "20"),
        new CodeSetting("Profile buckets", "20"),
        new CodeSetting("Profile lookback", "50 bars"),
        new CodeSetting("Decoding", "Softmax"),
        new CodeSetting("Temperature", "1.0")
    ];

    private IReadOnlyList<CodeSetting> TraderSettingsValues()
    {
        var config = _simulation.Snapshot?.Config;
        return config is null
            ?
            [
                new CodeSetting("Leverage", "1.0"),
                new CodeSetting("Stop out %", "50.0"),
                new CodeSetting("Commission %", "0.0")
            ]
            :
            [
                new CodeSetting("Leverage", config.Leverage.ToString("0.##", CultureInfo.InvariantCulture), IsLive: true),
                new CodeSetting("Stop out %", config.StopOutLevelPercent.ToString("0.##", CultureInfo.InvariantCulture), IsLive: true),
                new CodeSetting("Commission %", config.CommissionRatePercent.ToString("0.###", CultureInfo.InvariantCulture), IsLive: true)
            ];
    }

    private static string FormatSpan(TimeSpan span) =>
        span.TotalMinutes < 1
            ? $"{span.TotalSeconds:0}s"
            : span.TotalHours < 1 ? $"{span.TotalMinutes:0}m" : $"{span.TotalHours:0.##}h";

    private IReadOnlyList<CodeBadge> TrainingBadges()
    {
        var badges = new List<CodeBadge>
        {
            new("state", _training.IsRunning ? _training.IsPaused ? "PAUSED" : "RUNNING" : "IDLE")
        };

        if (_training.TotalEpisodes > 0)
            badges.Add(new CodeBadge("episodes", $"{_training.EpisodesCompleted} / {_training.TotalEpisodes}"));

        if (_training.EpisodesCompleted > 0)
            badges.Add(new CodeBadge("avg reward", _training.RecentAverageReward.ToString("N2", CultureInfo.InvariantCulture)));

        return badges;
    }

    private IReadOnlyList<CodeBadge> EpisodeRunnerBadges()
    {
        var completed = _training.RecentEpisodes.Count;
        return completed == 0 ? [] : [new CodeBadge("recent", completed.ToString(CultureInfo.InvariantCulture))];
    }

    private IReadOnlyList<CodeBadge> SimulationBadges() =>
        [new CodeBadge("replay", _simulation.Snapshot is null ? "not loaded" : "loaded")];

    private IReadOnlyList<CodeBadge> SamplerBadges()
    {
        var windows = _training.RecentEpisodeWindows.Count;
        return windows == 0 ? [] : [new CodeBadge("windows", windows.ToString(CultureInfo.InvariantCulture))];
    }

    private IReadOnlyList<CodeBadge> EncoderBadges()
    {
        var badges = new List<CodeBadge> { new("encoders", "6") };
        if (_training.LatestInputs is { Count: > 0 } inputs)
            badges.Add(new CodeBadge("features", inputs.Count.ToString(CultureInfo.InvariantCulture)));
        return badges;
    }

    private IReadOnlyList<CodeBadge> BrainBadges()
    {
        var options = _training.BrainOptions;
        var badges = new List<CodeBadge> { new("registered", options.Count.ToString(CultureInfo.InvariantCulture)) };
        var neural = options.Count(o => o.IsNeuralNetwork);
        if (neural > 0)
            badges.Add(new CodeBadge("neural", neural.ToString(CultureInfo.InvariantCulture)));
        return badges;
    }

    private IReadOnlyList<CodeBadge> DecoderBadges()
    {
        if (_training.LatestOutputs is not { Count: > 0 } outputs)
            return [];

        var directions = outputs.Count(o => o.Group == "Direction");
        return [new CodeBadge("outputs", $"{directions} of {outputs.Count}")];
    }

    private IReadOnlyList<CodeBadge> DecisionBadges()
    {
        var counts = _training.ActionCounts;
        var total = counts.Hold + counts.EnterLong + counts.EnterShort + counts.Close;
        return total == 0
            ? []
            : [new CodeBadge("episode", $"H {counts.Hold} L {counts.EnterLong} S {counts.EnterShort} C {counts.Close}")];
    }

    private IReadOnlyList<CodeBadge> RewardBadges() =>
        [new CodeBadge("policies", _training.RewardPolicyNames.Count.ToString(CultureInfo.InvariantCulture))];

    private IReadOnlyList<CodeBadge> StrategyBadges() =>
        [new CodeBadge("strategies", _training.StrategyNames.Count.ToString(CultureInfo.InvariantCulture))];
}
