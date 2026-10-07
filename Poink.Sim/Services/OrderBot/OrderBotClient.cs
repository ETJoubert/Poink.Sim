using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Poink.Sim.Services.OrderBot
{
	internal sealed class OrderBotClient : IOrderBotClient
	{
		public const string TokenHeader = "X-OrderBot-Token";

		private const string CommandsPath = "commands";
		private const string HealthPath = "health";

		private const string UnauthorizedMessage =
			"OrderBot rejected the command token. Make OrderBot:CommandToken match the bot's CommandToken parameter.";

		internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
		{
			Converters = { new JsonStringEnumConverter() },
		};

		private readonly HttpClient _http;
		private readonly OrderBotOptions _options;
		private readonly ILogger<OrderBotClient> _logger;

		public OrderBotClient(HttpClient http, IOptions<OrderBotOptions> options, ILogger<OrderBotClient> logger)
		{
			_http = http;
			_options = options.Value;
			_logger = logger;
		}

		private string UnreachableMessage =>
			$"OrderBot is not reachable at {_http.BaseAddress}. Start it in cTrader with AcceptExternalCommands enabled.";

		public async Task<OrderBotCommandResult> SendAsync(
			OrderBotCommand command,
			CancellationToken cancellationToken = default
		)
		{
			try
			{
				using var request = CreateRequest(HttpMethod.Post, CommandsPath);
				request.Content = JsonContent.Create(command, options: JsonOptions);
				using var response = await _http.SendAsync(request, cancellationToken);

				if (response.StatusCode == HttpStatusCode.Unauthorized)
					return new OrderBotCommandResult(false, UnauthorizedMessage);

				var result = await response.Content.ReadFromJsonAsync<OrderBotCommandResult>(
					JsonOptions,
					cancellationToken
				);
				return result
					?? new OrderBotCommandResult(
						false,
						$"OrderBot returned an empty reply ({(int)response.StatusCode})."
					);
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(
					ex,
					"OrderBot: unreachable at {BaseUrl} for command {CommandId}.",
					_http.BaseAddress,
					command.CommandId
				);
				return new OrderBotCommandResult(false, UnreachableMessage);
			}
			catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogError(
					ex,
					"OrderBot: no reply within {Timeout} for command {CommandId}.",
					_http.Timeout,
					command.CommandId
				);
				return new OrderBotCommandResult(
					false,
					$"No reply from OrderBot within {_http.Timeout.TotalSeconds:0}s. It may still have executed; check positions before retrying."
				);
			}
			catch (Exception ex) when (ex is JsonException or NotSupportedException)
			{
				_logger.LogError(ex, "OrderBot: unreadable reply for command {CommandId}.", command.CommandId);
				return new OrderBotCommandResult(
					false,
					"OrderBot returned an unreadable reply; check positions before retrying."
				);
			}
		}

		public async Task<OrderBotHealthResult> GetHealthAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				using var request = CreateRequest(HttpMethod.Get, HealthPath);
				using var response = await _http.SendAsync(request, cancellationToken);

				if (response.StatusCode == HttpStatusCode.Unauthorized)
					return new OrderBotHealthResult(null, UnauthorizedMessage);
				if (!response.IsSuccessStatusCode)
					return new OrderBotHealthResult(
						null,
						$"OrderBot health check failed ({(int)response.StatusCode})."
					);

				var health = await response.Content.ReadFromJsonAsync<OrderBotHealth>(JsonOptions, cancellationToken);
				return health is null
					? new OrderBotHealthResult(null, "OrderBot returned an empty health reply.")
					: new OrderBotHealthResult(health, "Online.");
			}
			catch (Exception ex)
				when (ex is HttpRequestException or JsonException or NotSupportedException
					|| (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
				)
			{
				return new OrderBotHealthResult(null, UnreachableMessage);
			}
		}

		private HttpRequestMessage CreateRequest(HttpMethod method, string path)
		{
			var request = new HttpRequestMessage(method, path);
			request.Headers.Add(TokenHeader, _options.CommandToken);
			return request;
		}
	}
}
