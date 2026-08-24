using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poink.CTrader;
using Poink.Sim.Brain;
using Poink.Sim.Components.Pages.Arch.Services;
using Poink.Sim.Components.Pages.Calls.Services;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Data;
using Poink.Sim.Repository;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;

namespace Poink.Sim.Services.Registration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// EF Postgres context plus every ITickRepository/ITRangeChartRepository/
    /// ITRangeReferenceChartRepository. Registered singleton to match ITickRepository's
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
            options.UseNpgsql(configuration.GetConnectionString("PoinkSim")));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PoinkSimContext>>().CreateDbContext());

        services.AddSingleton<ITickRepository, TickRepository>();
        services.AddSingleton<ITRangeChartRepository, TRangeChartRepository>();
        services.AddSingleton<ITRangeReferenceChartRepository, TRangeReferenceChartRepository>();

        return services;
    }

    /// <summary>
    /// cTrader Open API login/connection for the Trade page. Credentials bind from the
    /// "CTrader" config section (real values supplied via appsettings.Secrets.json -- see
    /// Poink.CTrader/README.md's "Secrets"). The session holds one live connection and its
    /// token in memory for this single-user dashboard.
    /// </summary>
    public static IServiceCollection BuildCTraderServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCTraderOpenApi(configuration);
        services.AddScoped<ICTraderTokenStore, PostgresCTraderTokenStore>();
        services.AddSingleton<CTraderSessionService>();
        services.AddSingleton<ICTraderSessionService>(sp => sp.GetRequiredService<CTraderSessionService>());
        services.AddSingleton<ICTraderHistory>(sp => sp.GetRequiredService<CTraderSessionService>());

        return services;
    }

    /// <summary>
    /// IHostedService workers: session restore on startup, and the single-writer tick
    /// backfill worker that persists live cTrader ticks into the PoinkMarketData
    /// ticks_&lt;symbol&gt; tables and fills the pre-stream gap from ProtoOAGetTickData.
    /// Depends on BuildCTraderServices for CTraderSessionService.
    /// </summary>
    public static IServiceCollection BuildBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<CTraderSessionRestoreService>();

        services.AddSingleton<ITickIngest, TickIngest>();
        services.AddSingleton<IHistoricalTickSource, CTraderHistoricalTickSource>();
        services.AddSingleton<TickBackfillWorker>();
        services.AddSingleton<ITickBackfillController>(sp => sp.GetRequiredService<TickBackfillWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<TickBackfillWorker>());

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
        services.AddSingleton<IBrainBuilder, BrainBuilder>();

        services.AddSingleton<IRiskPolicyService, RiskPolicyService>();
        services.AddSingleton<ITraderFactory, TraderFactory>();
        services.AddSingleton<ISimulationEngine, SimulationEngine>();

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
}
