// ═══════════════════════════════════════════════════════════════════════════
//  Poink.Sim — always-on Blazor Server dashboard for the trader simulation
//
//  Split out of Poink.ML because this isn't the training pipeline: it replays
//  historical ticks for a chosen symbol and date range under user control
//  (play/pause/step/speed), lets a pluggable ISimulationBrain observe the
//  stream (raw ticks or aggregated HLOC/AVDT bars) plus live Capital/Position,
//  and executes its decisions through Trader with simple cash P&L (no
//  leverage). See README.md for the full design.
//
//  Carries its own TorchSharp/Microsoft.ML (see Poink.Sim.csproj) for a future
//  trained ISimulationBrain, plus Postgres tick access (Repository/) and the
//  shared terminal UI (Poink.Shared.Client).
// ═══════════════════════════════════════════════════════════════════════════
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Poink.Sim.Brain;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Diagnostics;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Training;
using Poink.Sim.Transformer;
using Serilog;
using Serilog.Events;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Local, gitignored overrides (e.g. a real remote Postgres host/credentials) — see
// appsettings.json for the tracked local-dev connection strings and
// Poink.CTrader/README.md's "Secrets" section for the convention.
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Console + a local rolling file (Poink.Sim/poink-sim-.log, next to this
// file) kept for 14 days -- only DB calls and start/stop/load/save/reset
// state changes are logged, not per-tick/per-episode noise (see
// TODO/2026-07-22-logging.md). Microsoft.AspNetCore is overridden to
// Warning, otherwise Kestrel/routing/SignalR per-request logging floods a
// Blazor Server circuit at Information.
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(context.HostingEnvironment.ContentRootPath, "poink-sim-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

// Singleton: SimulationEngine holds one shared in-memory "current run" for the
// whole process (matching every other *TrainingService singleton in Poink.ML),
// so its dependencies must be singleton-safe too. ITickRepository also relies on
// being a singleton itself, to share its loaded-table-list cache across pages --
// SimulationEngine/RlTrainingService both read ticks straight from it (streamed,
// chunk-pruned range/window queries), never materializing a full range in memory.
builder.Services.AddSingleton<ITickRepository, TickRepository>();
// Brain Builder CRUD (topology-only "networks") -- see NetworkService/
// INetworkRepository's doc comments.
builder.Services.AddSingleton<IBrainRepository, NetworkRepository>();
builder.Services.AddSingleton<BrainService>();
// Trained-model CRUD (weights + run metadata) -- see ModelService/
// IModelRepository's doc comments.
builder.Services.AddSingleton<IModelRepository, ModelRepository>();
builder.Services.AddSingleton<ModelService>();
builder.Services.AddSingleton<ITRangeChartRepository, TRangeChartRepository>();
builder.Services.AddSingleton<ITRangeReferenceChartRepository, TRangeReferenceChartRepository>();
builder.Services.AddSingleton<ISimulationBrain, MomentumThresholdBrain>();
builder.Services.AddSingleton<ISimulationBrain, RandomBrain>();
// No hardcoded neural ISimulationBrain registrations here anymore -- every
// neural brain now comes from a saved NetworkService network, either built
// live via BrainBuilder (RlTrainingService.TryBuildBrain, which persists it
// as a network too) or lazily rebuilt into BrainRegistry from a saved
// network's recipe the first time TryStartTraining is asked to train it.
builder.Services.AddSingleton<BrainRegistry>();
// Stateless, no reason for a shorter lifetime -- Create is a pure passthrough
// to `new Trader.Trader(...)`.
builder.Services.AddSingleton<ITraderFactory, TraderFactory>();
builder.Services.AddSingleton<SimulationEngine>();
// Scoped (not Singleton like SimulationEngine): ProfilerService holds no shared
// "current run" state of its own -- each LoadAsync call is a self-contained batch
// read, so there's nothing that needs to be shared process-wide across circuits.
builder.Services.AddScoped<ProfilerService>();
// Same reasoning as ProfilerService above -- TransformerService is TransformerPage's
// own copy of the same batch-load pattern, registered through ITransformerService
// (unlike ProfilerService) for DI/testability.
builder.Services.AddScoped<ITransformerService, TransformerService>();
builder.Services.AddSingleton<IRlTrainingStrategy, PolicyGradientTrainingStrategy>();
// Order matters: RewardPolicyNames[0] (this first registration) is what
// TrainPage.razor.cs's OnInitialized preselects, so the baseline/unshaped
// policy -- not Trading Rules -- stays the default a fresh page load starts
// on.
builder.Services.AddSingleton<IRewardPolicy, EquityDriftRewardPolicy>();
builder.Services.AddSingleton<IRewardPolicy, TradingRulesRewardPolicy>();
builder.Services.AddSingleton<IRewardPolicy, DifferentialSharpeRewardPolicy>();
builder.Services.AddSingleton<IRewardPolicy, MarketStructureRewardPolicy>();
builder.Services.AddSingleton<RlTrainingService>();
builder.Services.AddScoped<ICodeGraphProvider, CodeGraphProvider>();
#pragma warning disable CA1416 // SystemPerformanceMonitor is Windows-only, same as this app's other host assumptions (see its class comment).
builder.Services.AddSingleton<SystemPerformanceMonitor>();
#pragma warning restore CA1416
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<Poink.Sim.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
