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
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Poink.CTrader;
using Poink.Sim.Brain;
using Poink.Sim.Components.Pages.Arch.Services;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Data;
using Poink.Sim.Diagnostics;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Training;
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
        Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "poink-sim-.log"),
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
builder.Services.AddSingleton<IBrainService, BrainService>();
// Trained-model CRUD (weights + run metadata) -- see ModelService/
// IModelRepository's doc comments.
builder.Services.AddSingleton<IModelRepository, ModelRepository>();
builder.Services.AddSingleton<IModelService, ModelService>();
builder.Services.AddSingleton<ITRangeChartRepository, TRangeChartRepository>();
builder.Services.AddSingleton<ITRangeReferenceChartRepository, TRangeReferenceChartRepository>();
builder.Services.AddSingleton<ISimulationBrain, MomentumThresholdBrain>();
builder.Services.AddSingleton<ISimulationBrain, RandomBrain>();

builder.Services.AddSingleton<BrainRegistry>();

builder.Services.AddSingleton<IRiskPolicyService, RiskPolicyService>();
builder.Services.AddSingleton<ITraderFactory, TraderFactory>();
builder.Services.AddSingleton<ISimulationEngine, SimulationEngine>();

// cTrader Open API login/connection for the Trade page. Credentials bind from the
// "CTrader" config section (real values supplied via appsettings.Secrets.json -- see
// Poink.CTrader/README.md's "Secrets"). The session holds one live connection and its
// token in memory for this single-user dashboard.
builder.Services.AddCTraderOpenApi(builder.Configuration);
builder.Services.AddDbContext<PoinkSimContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PoinkSim")));
builder.Services.AddScoped<ICTraderTokenStore, PostgresCTraderTokenStore>();
builder.Services.AddSingleton<CTraderSessionService>();
builder.Services.AddSingleton<ICTraderSessionService>(sp => sp.GetRequiredService<CTraderSessionService>());
builder.Services.AddSingleton<ICTraderHistory>(sp => sp.GetRequiredService<CTraderSessionService>());
builder.Services.AddHostedService<CTraderSessionRestoreService>();

// Tick backfill: single-writer worker that persists live cTrader ticks into the
// PoinkMarketData ticks_<symbol> tables and fills the pre-stream gap from ProtoOAGetTickData.
builder.Services.AddSingleton<ITickIngest, TickIngest>();
builder.Services.AddSingleton<IHistoricalTickSource, CTraderHistoricalTickSource>();
builder.Services.AddSingleton<TickBackfillWorker>();
builder.Services.AddSingleton<ITickBackfillController>(sp => sp.GetRequiredService<TickBackfillWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TickBackfillWorker>());

builder.Services.AddScoped<IProfilerService, ProfilerService>();

builder.Services.AddSingleton<IRlTrainingStrategy, PolicyGradientTrainingStrategy>();

builder.Services.AddSingleton<IRewardPolicyService, RewardPolicyService>();
builder.Services.AddSingleton<IRlTrainingService, RlTrainingService>();
builder.Services.AddSingleton<ICodeSourceIndex, CodeSourceIndex>();
builder.Services.AddScoped<ICodeGraphProvider, CodeGraphProvider>();
builder.Services.AddSingleton<IXmlDocumentationProvider, XmlDocumentationProvider>();
builder.Services.AddScoped<IArchGraphProvider, ArchGraphProvider>();
// Snapshot of every registration made above this line, so the Arch page can reflect the
// DI container itself into a graph instead of maintaining a hand-written node list.
builder.Services.AddSingleton<IReadOnlyList<ServiceDescriptor>>(builder.Services.ToList());
#pragma warning disable CA1416 // SystemPerformanceMonitor is Windows-only, same as this app's other host assumptions (see its class comment).
builder.Services.AddSingleton<SystemPerformanceMonitor>();
#pragma warning restore CA1416
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<PoinkSimContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Poink.Sim database migrations applied.");
}

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

app.MapCTraderAuthCallback();

app.Run();
