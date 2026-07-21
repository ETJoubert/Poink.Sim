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

var builder = WebApplication.CreateBuilder(args);

// Singleton: SimulationEngine holds one shared in-memory "current run" for the
// whole process (matching every other *TrainingService singleton in Poink.ML),
// so its dependencies must be singleton-safe too. ITickRepository also relies on
// being a singleton itself, to share its loaded-table-list cache across pages.
// TickDataStore is the same idea one level up: it holds the actual loaded tick
// data (not just table metadata), shared by SimulationPage/TrainPage's "LOAD
// DATA" button so neither page -- nor SimulationEngine/RlTrainingService, which
// no longer touch Postgres themselves -- re-reads the same range twice.
builder.Services.AddSingleton<ITickRepository, TickRepository>();
builder.Services.AddSingleton<IBrainRepository, BrainRepository>();
builder.Services.AddSingleton<TickDataStore>();
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
