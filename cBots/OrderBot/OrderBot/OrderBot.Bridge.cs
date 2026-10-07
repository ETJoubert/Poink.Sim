using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace cAlgo.Robots;

/// <summary>
/// Localhost HTTP bridge: GET /health and POST /commands, both requiring the <c>X-OrderBot-Token</c> header to match
/// <c>CommandToken</c>. Commands run on the cBot's main thread; a repeated command id returns the first result
/// instead of trading twice.
/// </summary>
public partial class OrderBot
{
	private const string BotName = "OrderBot";
	private const string TokenHeader = "X-OrderBot-Token";

	// Answers before Poink.Sim's own request timeout so a slow main thread still reports back.
	private static readonly TimeSpan MainThreadWait = TimeSpan.FromSeconds(9);

	private static readonly TimeSpan CommandRetention = TimeSpan.FromMinutes(10);

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
	};

	private readonly ConcurrentDictionary<Guid, (DateTime ReceivedAtUtc, Task<BotCommandResult> Result)> _commands =
		new();

	private HttpListener _listener;
	private CancellationTokenSource _bridgeCancellation;
	private volatile bool _acceptingCommands;

	private void StartBridge()
	{
		if (string.IsNullOrWhiteSpace(CommandToken))
		{
			Print(
				"Bridge not started: set CommandToken here and OrderBot:CommandToken in Poink.Sim's appsettings.Secrets.json to the same secret."
			);
			Stop();
			return;
		}

		var prefix = $"http://localhost:{ListenPort}/";
		var listener = new HttpListener();
		listener.Prefixes.Add(prefix);
		try
		{
			listener.Start();
		}
		catch (HttpListenerException ex)
		{
			Print("Bridge not started: cannot listen on {0} ({1}).", prefix, ex.Message);
			Stop();
			return;
		}

		_listener = listener;
		_bridgeCancellation = new CancellationTokenSource();
		_acceptingCommands = true;
		_ = Task.Run(() => AcceptLoopAsync(listener, _bridgeCancellation.Token));
		Print(
			"Bridge listening on {0} for account {1} ({2}).",
			prefix,
			_accountNumber,
			_accountIsLive ? "LIVE" : "DEMO"
		);
	}

	private void StopBridge()
	{
		if (_listener is null)
			return;

		_acceptingCommands = false;
		_bridgeCancellation.Cancel();
		_listener.Close();
		_listener = null;
		Print("Bridge stopped.");
	}

	private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			HttpListenerContext context;
			try
			{
				context = await listener.GetContextAsync();
			}
			catch (Exception) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
			{
				return;
			}

			_ = Task.Run(() => HandleRequestAsync(context));
		}
	}

	private async Task HandleRequestAsync(HttpListenerContext context)
	{
		try
		{
			if (!IsAuthorized(context.Request))
			{
				await WriteJsonAsync(
					context,
					HttpStatusCode.Unauthorized,
					new BotCommandResult(false, "Invalid command token.")
				);
				return;
			}

			var method = context.Request.HttpMethod;
			var path = context.Request.Url?.AbsolutePath.Trim('/') ?? string.Empty;

			if (method == "GET" && path == "health")
				await WriteJsonAsync(
					context,
					HttpStatusCode.OK,
					new BotHealth(BotName, _accountNumber, _accountIsLive, _brokerName, _acceptingCommands)
				);
			else if (method == "POST" && path == "commands")
				await WriteJsonAsync(context, HttpStatusCode.OK, await ReceiveCommandAsync(context.Request));
			else
				await WriteJsonAsync(
					context,
					HttpStatusCode.NotFound,
					new BotCommandResult(false, $"Unknown endpoint {method} /{path}.")
				);
		}
		catch (Exception ex)
		{
			BeginInvokeOnMainThread(() => Print("Bridge request failed: {0}", ex.Message));
			await TryWriteJsonAsync(
				context,
				HttpStatusCode.InternalServerError,
				new BotCommandResult(false, $"OrderBot error: {ex.Message}")
			);
		}
	}

	private async Task<BotCommandResult> ReceiveCommandAsync(HttpListenerRequest request)
	{
		BotCommand command;
		try
		{
			command = await JsonSerializer.DeserializeAsync<BotCommand>(request.InputStream, JsonOptions);
		}
		catch (JsonException ex)
		{
			return new BotCommandResult(false, $"Malformed command: {ex.Message}");
		}

		if (command is null)
			return new BotCommandResult(false, "Empty command.");
		if (!_acceptingCommands)
			return new BotCommandResult(false, "OrderBot is shutting down.");
		if (command.AccountNumber != _accountNumber)
			return new BotCommandResult(
				false,
				$"Command is for account {command.AccountNumber}, but OrderBot runs on account {_accountNumber}."
			);

		PruneCommands();
		var completion = new TaskCompletionSource<BotCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!_commands.TryAdd(command.CommandId, (DateTime.UtcNow, completion.Task)))
			return await _commands[command.CommandId].Result;

		BeginInvokeOnMainThread(() => completion.TrySetResult(ExecuteOnMainThread(command)));

		var finished = await Task.WhenAny(completion.Task, Task.Delay(MainThreadWait));
		return finished == completion.Task
			? completion.Task.Result
			: new BotCommandResult(
				false,
				"OrderBot's main thread is busy; the command expires unexecuted unless it starts within its lifetime. Check positions."
			);
	}

	private BotCommandResult ExecuteOnMainThread(BotCommand command)
	{
		BotCommandResult result;
		if (DateTime.UtcNow > command.ExpiresAtUtc)
			result = new BotCommandResult(
				false,
				$"Command expired at {command.ExpiresAtUtc:HH:mm:ss.fff} UTC before it could run; nothing was sent."
			);
		else
		{
			try
			{
				result = Execute(command);
			}
			catch (Exception ex)
			{
				result = new BotCommandResult(false, $"OrderBot failed: {ex.Message}");
			}
		}

		Print(
			"{0} {1} {2}: {3} {4}",
			command.Kind,
			command.SymbolName ?? command.PositionId?.ToString(),
			command.CommandId.ToString("N")[..8],
			result.Success ? "OK" : "FAILED",
			result.Message
		);
		return result;
	}

	private bool IsAuthorized(HttpListenerRequest request)
	{
		var supplied = Encoding.UTF8.GetBytes(request.Headers[TokenHeader] ?? string.Empty);
		var expected = Encoding.UTF8.GetBytes(CommandToken);
		return CryptographicOperations.FixedTimeEquals(supplied, expected);
	}

	private void PruneCommands()
	{
		var cutoff = DateTime.UtcNow - CommandRetention;
		foreach (var stale in _commands.Where(c => c.Value.ReceivedAtUtc < cutoff).Select(c => c.Key).ToList())
			_commands.TryRemove(stale, out _);
	}

	private static async Task WriteJsonAsync<T>(HttpListenerContext context, HttpStatusCode status, T body)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
		context.Response.StatusCode = (int)status;
		context.Response.ContentType = "application/json; charset=utf-8";
		context.Response.ContentLength64 = bytes.Length;
		await context.Response.OutputStream.WriteAsync(bytes);
		context.Response.Close();
	}

	private static async Task TryWriteJsonAsync<T>(HttpListenerContext context, HttpStatusCode status, T body)
	{
		try
		{
			await WriteJsonAsync(context, status, body);
		}
		catch (Exception)
		{
			context.Response.Abort();
		}
	}
}
