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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Diagnostics;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Training;
using Serilog;
using Serilog.Events;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IBrainRepository, BrainRepository>();
builder.Services.AddSingleton<ISimulationBrain, MomentumThresholdBrain>();
builder.Services.AddSingleton<ISimulationBrain, RandomBrain>();
// Registered as ISimulationBrain (not just IPolicyGradientBrain) so it shows up in
// the existing simulation setup screen's Brain dropdown immediately -- untrained
// at first, improving as RlTrainingService trains this same singleton instance.
builder.Services.AddSingleton<ISimulationBrain, MlpPolicyBrain>();
builder.Services.AddSingleton<ISimulationBrain, LstmPolicyBrain>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddSingleton<IRlTrainingStrategy, PolicyGradientTrainingStrategy>();
builder.Services.AddSingleton<RlTrainingService>();
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
