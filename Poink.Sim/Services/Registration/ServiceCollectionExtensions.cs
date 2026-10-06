using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poink.CTrader;
using Poink.Sim.Analysis;
using Poink.Sim.Brain;
using Poink.Sim.Components.Pages.Arch.Services;
using Poink.Sim.Components.Pages.Calls.Services;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Components.Pages.Trade;
using Poink.Sim.Data;
using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Bots;
using Poink.Sim.Helpers;
using Poink.Sim.Repository;
using Poink.Sim.Services.Activity;
using Poink.Sim.Services.Activity.Sources;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.DataSeriesSync;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Services.MarketData.Import;
using Poink.Sim.Services.MarketData.Recording;
using Poink.Sim.Services.Symbols;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Services.Registration;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// EF Postgres context plus every ITickRepository/ITRangeChartRepository/
	/// ITRangeReferenceChartRepository/IAnalysisHistoryRepository. Registered singleton to match ITickRepository's
	/// requirement to share its loaded-table-list cache across pages --
	/// SimulationEngine/RlTrainingService both read ticks straight from it (streamed,
	/// chunk-pruned range/window queries), never materializing a full range in memory.
	/// Only AddDbContextFactory is called (not AddDbContext) so its DbContextOptions stay
	/// singleton-safe -- IDbContextFactory&lt;PoinkSimContext&gt; is what NeuralNetwork/
	/// NeuralWeights repositories use to be singletons themselves (see BuildTrainingServices).
	/// A scoped PoinkSimContext is then derived from that same factory for services (e.g.
	/// TrainingResultService, PostgresCTraderTokenStore) that still inject the context directly.
	/// </summary>
	public static IServiceCollection BuildDatabase(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContextFactory<PoinkSimContext>(options =>
			options.UseNpgsql(configuration.GetConnectionString("PoinkSim"))
		);
		services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PoinkSimContext>>().CreateDbContext());

		services.AddSingleton<ITickRepository, TickRepository>();
		services.AddSingleton<ITRangeChartRepository, TRangeChartRepository>();
		services.AddSingleton<ITRangeReferenceChartRepository, TRangeReferenceChartRepository>();
		services.AddSingleton<IAnalysisHistoryRepository, AnalysisHistoryRepository>();

		return services;
	}

	/// <summary>
	/// cTrader Open API login/connection for the Trade page. Credentials bind from the
	/// "CTrader" config section (real values supplied via appsettings.Secrets.json -- see
	/// Poink.CTrader/README.md's "Secrets"). The session holds one live connection and its
	/// token in memory for this single-user dashboard. Also registers the DB-first closed-deal
	/// history cache (ITradeHistoryService), backed by the singleton ITradeDealRepository since
	/// it must share TradeDealHistoryService's process-wide lifetime (see BuildDatabase).
	/// </summary>
	public static IServiceCollection BuildCTraderServices(
		this IServiceCollection services,
		IConfiguration configuration
	)
	{
		services.AddCTraderOpenApi(configuration);
		services.AddScoped<ICTraderTokenStore, PostgresCTraderTokenStore>();
		services.AddSingleton<CTraderSessionService>();
		services.AddSingleton<ICTraderSessionService>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<ICTraderHistory>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<IWatchlistAccess>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<ITickStreamAccess>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<ITradeExecutionAccess>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<ITradeHistoryAccess>(sp => sp.GetRequiredService<CTraderSessionService>());
		services.AddSingleton<IAccountAccess>(sp => sp.GetRequiredService<CTraderSessionService>());

		services.AddSingleton<ITradeDealRepository, TradeDealRepository>();
		services.AddSingleton<TradeDealHistoryService>();
		services.AddSingleton<ITradeHistoryService>(sp => sp.GetRequiredService<TradeDealHistoryService>());

		return services;
	}

	/// <summary>
	/// Symbol registry: persists the connected cTrader account's symbol list into the Symbol
	/// table so it can be browsed on the Data page without an active session. Scoped, to
	/// match its PoinkSimContext dependency (see BuildDatabase). Also registers the Trade
	/// page's symbol metadata cache, shared across its components (watchlist, order entry,
	/// financials, chart) so each only fetches a symbol's pip size/digits once.
	/// </summary>
	public static IServiceCollection BuildSymbolServices(this IServiceCollection services)
	{
		services.AddScoped<ISymbolService, SymbolService>();
		services.AddScoped<ISymbolMetadataCache, SymbolMetadataCacheService>();
		services.AddScoped<ISymbolTableResolver, SymbolTableResolver>();

		return services;
	}

	/// <summary>
	/// TradePage's cross-panel UI state (selected symbol, side-panel collapse, settings persistence) and the per-circuit bar feed --
	/// scoped, to match its <see cref="ILocalStorageHelper"/> and driver-interface dependencies.
	/// </summary>
	public static IServiceCollection BuildTradePageServices(this IServiceCollection services)
	{
		services.AddScoped<ITradeCoordinator, TradeCoordinator>();
		services.AddScoped<ITradeDraftService, TradeDraftService>();
		services.AddScoped<ITradeBarFeed, TradeBarFeed>();

		return services;
	}

	/// <summary>
	/// IHostedService workers: session restore on startup. The tick backfill worker (live
	/// streaming + gap-fill) was removed pending a rebuild -- ITickIngest stays registered since
	/// ZTicksImportService still writes through it and DataSeriesSyncService reads its coverage. The Trade page's
	/// database-first tick history and its live tick recorder also write through it.
	/// Depends on BuildCTraderServices for CTraderSessionService.
	/// </summary>
	public static IServiceCollection BuildBackgroundWorkers(this IServiceCollection services)
	{
		services.AddHostedService<CTraderSessionRestoreService>();

		services.AddSingleton<ITickIngest, TickIngest>();
		services.AddSingleton<IZTicksImportService, ZTicksImportService>();
		services.AddSingleton<IHistoricalTickSource, CTraderHistoricalTickSource>();
		services.AddSingleton<ITickHistoryService, TickHistoryService>();
		services.AddSingleton<ILiveTickRecorder, LiveTickRecorder>();
		services.AddSingleton<ITodayTickReloadService, TodayTickReloadService>();

		return services;
	}

	/// <summary>
	/// Read-only "database coverage vs. what's left to load" view over the tick store, backing
	/// DataSeriesSyncChart. Depends on ITickIngest's coverage query, so is registered after
	/// BuildBackgroundWorkers.
	/// </summary>
	public static IServiceCollection BuildDataSeriesSyncServices(this IServiceCollection services)
	{
		services.AddSingleton<IDataSeriesSyncService, DataSeriesSyncService>();

		return services;
	}

	/// <summary>
	/// RL training pipeline plus the Brain/Model CRUD service layer (topology-only
	/// "networks" and trained weights + run metadata) that the Brain Builder and
	/// Training pages sit on top of. Also registers the EF-backed TrainingResult service
	/// (scoped, to match its PoinkSimContext dependency) and the NeuralNetwork/NeuralWeights
	/// create/read/delete services (singleton, backed by IDbContextFactory -- see
	/// BuildDatabase).
	/// </summary>
	public static IServiceCollection BuildTrainingServices(this IServiceCollection services)
	{
		services.AddSingleton<IBrainService, BrainService>();
		services.AddSingleton<IModelService, ModelService>();

		services.AddSingleton<IRlTrainingStrategy, PolicyGradientTrainingStrategy>();
		services.AddSingleton<IRlTrainingStrategy, PerDecisionPolicyGradientTrainingStrategy>();
		services.AddSingleton<IRlTrainingStrategy, NeatTrainingStrategy>();
		services.AddSingleton<IRewardPolicyService, RewardPolicyService>();
		services.AddSingleton<ITrainingService, TrainingService>();

		services.AddScoped<ITrainingResultService, TrainingResultService>();

		services.AddSingleton<INeuralNetworkRepository, NeuralNetworkRepository>();
		services.AddSingleton<INeuralNetworkService, NeuralNetworkService>();

		services.AddSingleton<INeuralWeightsRepository, NeuralWeightsRepository>();
		services.AddSingleton<INeuralWeightsService, NeuralWeightsService>();

		return services;
	}

	/// <summary>
	/// TrainPageV2's rebuilt training backend -- a fresh pipeline (data provider, feature/action/
	/// network assemblers, decision masking, training engine) that shares no code with
	/// BuildTrainingServices' BrainCatalog-based stack above, by design. Concrete classes only, no
	/// interfaces, since each has exactly one implementation.
	/// </summary>
	public static IServiceCollection BuildTrainingEngineServices(this IServiceCollection services)
	{
		services.AddSingleton<DataProvider>();
		services.AddSingleton<FeatureAssembler>();
		services.AddSingleton<ActionAssembler>();
		services.AddSingleton<NetworkAssembler>();
		services.AddSingleton<PolicyGradientLearner>();
		services.AddSingleton<TrainingEngineService>();
		services.AddSingleton<TrainingRunService>();
		services.AddSingleton<MlpWeightsSerializer>();
		services.AddSingleton<TrainedModelRepository>();
		services.AddSingleton<TrainingHistoryRepository>();
		services.AddSingleton<TrainedModelService>();
		services.AddSingleton<TrainingHistoryService>();

		return services;
	}

	/// <summary>
	/// Tick replay engine, its pluggable ISimulationBrain implementations, and the
	/// Trader/risk pipeline that executes brain decisions with simple cash P&amp;L
	/// (no leverage). SimulationEngine is a process-wide singleton "current run",
	/// so its dependencies must be singleton-safe too.
	/// </summary>
	public static IServiceCollection BuildSimulationServices(this IServiceCollection services)
	{
		services.AddSingleton<ISimulationBrain, MomentumThresholdBrain>();
		services.AddSingleton<ISimulationBrain, RandomBrain>();
		services.AddSingleton<BrainRegistry>();

		services.AddSingleton<IRiskPolicyService, RiskPolicyService>();
		services.AddSingleton<ITraderFactory, TraderFactory>();
		services.AddSingleton<Bots.IBotService, Bots.BotService>();
		services.AddSingleton<ISimulationEngine, SimulationEngine>();
		services.AddSingleton<ISignalDatasetRepository, SignalDatasetRepository>();
		services.AddSingleton<SignalStudy.ISignalDatasetBuilder, SignalStudy.SignalDatasetBuilder>();
		services.AddSingleton<SignalStudy.ISignalDatasetService, SignalStudy.SignalDatasetService>();
		services.AddSingleton<SignalStudy.ISignalAnalysisService, SignalStudy.SignalAnalysisService>();

		return services;
	}

	/// <summary>
	/// The Emulator page's brokerage emulator: one process-wide synthetic market, so it keeps stepping across page
	/// visits. The bot strategy is only ever called under the emulator's step lock, so one singleton serves every bot.
	/// </summary>
	public static IServiceCollection BuildEmulatorServices(this IServiceCollection services)
	{
		services.AddSingleton<IEmulatorBotStrategy, RandomBotStrategy>();
		services.AddSingleton<IEmulatorService, EmulatorService>();

		return services;
	}

	/// <summary>
	/// Dev-tooling pages that reflect the codebase itself: the Code page's source index
	/// and dependency graph, and the Arch page's DI-container graph (labelled from XML
	/// doc comments via IXmlDocumentationProvider).
	/// </summary>
	public static IServiceCollection BuildCodeGraphServices(this IServiceCollection services)
	{
		services.AddSingleton<ICodeSourceIndex, CodeSourceIndex>();
		services.AddScoped<ICodeGraphProvider, CodeGraphProvider>();
		services.AddSingleton<IXmlDocumentationProvider, XmlDocumentationProvider>();
		services.AddScoped<IArchGraphProvider, ArchGraphProvider>();

		return services;
	}

	/// <summary>
	/// The Calls page's method-level call graph, built by parsing Poink.Sim's own source with
	/// Roslyn (see CallGraphIndex.cs) — the one thing reflection-based Arch/Code cannot show.
	/// </summary>
	public static IServiceCollection BuildCallGraphServices(this IServiceCollection services)
	{
		services.AddSingleton<ICallGraphIndex, CallGraphIndex>();

		return services;
	}

	/// <summary>
	/// Background-activity indicators for the main nav: one IActivitySource per long-running singleton service
	/// (simulation, training, tick import, signal dataset build, each analysis) and the tracker that folds them into per-route state.
	/// Depends on BuildSimulationServices, BuildTrainingServices, BuildBackgroundWorkers and the analysis services.
	/// </summary>
	public static IServiceCollection BuildActivityServices(this IServiceCollection services)
	{
		services.AddSingleton<IActivitySource, SimulationActivitySource>();
		services.AddSingleton<IActivitySource, TrainingActivitySource>();
		services.AddSingleton<IActivitySource, TrainingRunActivitySource>();
		services.AddSingleton<IActivitySource, DataImportActivitySource>();
		services.AddSingleton<IActivitySource, SignalDatasetActivitySource>();
		services.AddSingleton<IActivitySource>(sp =>
			new AnalysisActivitySource("Time above/below", sp.GetRequiredService<ITimeAboveBelowAnalysisService>())
		);
		services.AddSingleton<IActivitySource>(sp =>
			new AnalysisActivitySource("Time to return", sp.GetRequiredService<IPriceReturnAnalysisService>())
		);
		services.AddSingleton<IActivitySource>(sp =>
			new AnalysisActivitySource("Barrier map", sp.GetRequiredService<IBarrierMapAnalysisService>())
		);
		services.AddSingleton<IActivityTracker, ActivityTracker>();

		return services;
	}
}
