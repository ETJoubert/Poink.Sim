using System.IO;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;

namespace Poink.Sim.Services.Registration;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Console + a local rolling file (Poink.Sim/logs/poink-sim-.log) kept for 14 days.
    /// Only DB calls and start/stop/load/save/reset state changes are logged, not
    /// per-tick/per-episode noise (see TODO/2026-07-22-logging.md). Microsoft.AspNetCore
    /// is overridden to Warning, otherwise Kestrel/routing/SignalR per-request logging
    /// floods a Blazor Server circuit at Information.
    /// </summary>
    public static WebApplicationBuilder SetupLogging(this WebApplicationBuilder builder)
    {
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

        return builder;
    }
}
