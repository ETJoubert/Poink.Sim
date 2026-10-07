using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>Probes the OrderBot's health every poll interval while a live account is selected, logging only state transitions.</summary>
	internal sealed class OrderBotStatusMonitor : BackgroundService, IOrderBotStatus
	{
		private readonly IOrderBotClient _client;
		private readonly IAccountAccess _accounts;
		private readonly OrderBotOptions _options;
		private readonly ILogger<OrderBotStatusMonitor> _logger;

		public OrderBotStatusMonitor(
			IOrderBotClient client,
			IAccountAccess accounts,
			IOptions<OrderBotOptions> options,
			ILogger<OrderBotStatusMonitor> logger
		)
		{
			_client = client;
			_accounts = accounts;
			_options = options.Value;
			_logger = logger;
		}

		public OrderBotStatus Status { get; private set; } = OrderBotStatus.Idle;

		public event Action? Changed;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation(
				"OrderBot status monitor started: route live orders {RouteLiveOrders}, bot at {BaseUrl}, polling every {PollSeconds}s.",
				_options.RouteLiveOrders,
				_options.BaseUrl,
				_options.StatusPollSeconds
			);

			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.StatusPollSeconds)));
			try
			{
				do
				{
					await RefreshAsync(stoppingToken);
				} while (await timer.WaitForNextTickAsync(stoppingToken));
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }

			_logger.LogInformation("OrderBot status monitor stopped.");
		}

		private async Task RefreshAsync(CancellationToken cancellationToken)
		{
			var next = OrderBotRoutingHelper.TryGetBotAccount(_accounts, _options, out var account)
				? OrderBotStatusHelper.Evaluate(await _client.GetHealthAsync(cancellationToken), account)
				: OrderBotStatus.Idle;
			if (next == Status)
				return;

			Status = next;
			LogTransition(next);
			Changed?.Invoke();
		}

		private void LogTransition(OrderBotStatus status)
		{
			if (status.State == OrderBotConnectionState.Offline)
				_logger.LogWarning("OrderBot offline: {Message}", status.Message);
			else
				_logger.LogInformation("OrderBot {State}: {Message}", status.State, status.Message);
		}
	}
}
