// Split out of Poink.ML: the always-on dashboard, not the training pipeline (see README.md).
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Diagnostics;
using Poink.Sim.Helpers;
using Poink.Sim.Profiler;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.Registration;

var builder = WebApplication.CreateBuilder(args);

// Local, gitignored overrides; see Poink.CTrader/README.md's "Secrets" section.
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

builder.SetupLogging();

builder.Services.BuildDatabase(builder.Configuration);
builder.Services.BuildCTraderServices(builder.Configuration);
builder.Services.BuildSymbolServices();
builder.Services.BuildBackgroundWorkers();
builder.Services.BuildTrainingServices();
builder.Services.BuildSimulationServices();

builder.Services.AddSingleton<IProfilerService, ProfilerService>();
builder.Services.AddScoped<ILocalStorageHelper, LocalStorageHelper>();
builder.Services.BuildCodeGraphServices();
builder.Services.BuildCallGraphServices();

// Snapshot of every registration above, so the Arch page can graph the DI container itself.
builder.Services.AddSingleton<IReadOnlyList<ServiceDescriptor>>(builder.Services.ToList());

#pragma warning disable CA1416 // SystemPerformanceMonitor is Windows-only, same as this app's other host assumptions (see its class comment).
builder.Services.AddSingleton<SystemPerformanceMonitor>();
#pragma warning restore CA1416
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

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

app.MapRazorComponents<Poink.Sim.Components.App>().AddInteractiveServerRenderMode();

app.MapCTraderAuthCallback();

app.Run();
