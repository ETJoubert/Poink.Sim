using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// Restores any persisted cTrader session in the background at startup, so a prior login reconnects
	/// without blocking application boot or requiring a fresh OAuth redirect.
	/// </summary>
	public sealed class CTraderSessionRestoreService : IHostedService
	{
		private readonly ICTraderSessionService _session;
		private readonly ILogger<CTraderSessionRestoreService> _logger;

		public CTraderSessionRestoreService(
			ICTraderSessionService session,
			ILogger<CTraderSessionRestoreService> logger
		)
		{
			_session = session;
			_logger = logger;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_ = Task.Run(
				async () =>
				{
					try
					{
						await _session.TryRestoreAsync();
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "cTrader: background session restore failed.");
					}
				},
				cancellationToken
			);

			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
