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
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

var builder = WebApplication.CreateBuilder(args);

// Singleton: SimulationEngine holds one shared in-memory "current run" for the
// whole process (matching every other *TrainingService singleton in Poink.ML),
// so its dependencies must be singleton-safe too. ITickRepository is stateless.
builder.Services.AddSingleton<ITickRepository, TickRepository>();
builder.Services.AddSingleton<ISimulationBrain, MomentumThresholdBrain>();
builder.Services.AddSingleton<SimulationEngine>();
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
