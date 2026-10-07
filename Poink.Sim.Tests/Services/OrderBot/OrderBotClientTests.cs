using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.OrderBot;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.OrderBot;

public class OrderBotClientTests
{
	private const string Token = "s3cret";

	private static readonly OrderBotCommand Command = new(
		Guid.NewGuid(),
		19646985,
		OrderBotCommandKind.ClosePosition,
		new DateTime(2026, 10, 7, 9, 0, 5, DateTimeKind.Utc),
		PositionId: 42
	);

	private static (OrderBotClient Client, StubHttpHandler Handler) CreateClient(
		Func<HttpRequestMessage, HttpResponseMessage> respond
	)
	{
		var handler = new StubHttpHandler(respond);
		var http = new HttpClient(handler)
		{
			BaseAddress = new Uri("http://localhost:5099/"),
			Timeout = TimeSpan.FromSeconds(10),
		};
		var client = new OrderBotClient(
			http,
			Options.Create(new OrderBotOptions { CommandToken = Token }),
			NullLogger<OrderBotClient>.Instance
		);
		return (client, handler);
	}

	private static HttpResponseMessage Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK) =>
		new(status) { Content = JsonContent.Create(body, options: OrderBotClient.JsonOptions) };

	[Fact]
	public async Task SendAsync_PostsTheCommandWithTheTokenAndReturnsTheBotsResult()
	{
		var (client, handler) = CreateClient(_ => Json(new OrderBotCommandResult(true, "Position 42 closed.", 42)));

		var result = await client.SendAsync(Command);

		Assert.True(result.Success);
		Assert.Equal("Position 42 closed.", result.Message);
		var (request, body) = Assert.Single(handler.Requests);
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("http://localhost:5099/commands", request.RequestUri!.ToString());
		Assert.Equal(Token, request.Headers.GetValues(OrderBotClient.TokenHeader).Single());
		Assert.Contains("\"kind\":\"ClosePosition\"", body);
		Assert.Contains("\"positionId\":42", body);
	}

	[Fact]
	public async Task SendAsync_Unauthorized_ExplainsTheTokenMismatch()
	{
		var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

		var result = await client.SendAsync(Command);

		Assert.False(result.Success);
		Assert.Contains("CommandToken", result.Message);
	}

	[Fact]
	public async Task SendAsync_BotNotRunning_ReportsItUnreachable()
	{
		var (client, _) = CreateClient(_ => throw new HttpRequestException("Connection refused"));

		var result = await client.SendAsync(Command);

		Assert.False(result.Success);
		Assert.Contains("not reachable", result.Message);
	}

	[Fact]
	public async Task SendAsync_NoReply_WarnsToCheckPositions()
	{
		var (client, _) = CreateClient(_ => throw new TaskCanceledException());

		var result = await client.SendAsync(Command);

		Assert.False(result.Success);
		Assert.Contains("check positions", result.Message);
	}

	[Fact]
	public async Task SendAsync_UnreadableReply_WarnsToCheckPositions()
	{
		var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("<html>"),
		});

		var result = await client.SendAsync(Command);

		Assert.False(result.Success);
		Assert.Contains("check positions", result.Message);
	}

	[Fact]
	public async Task GetHealthAsync_ReturnsTheBotsHealth()
	{
		var health = new OrderBotHealth("OrderBot", 19646985, true, "Broker", true);
		var (client, handler) = CreateClient(_ => Json(health));

		var result = await client.GetHealthAsync();

		Assert.Equal(health, result.Health);
		Assert.Equal("http://localhost:5099/health", Assert.Single(handler.Requests).Request.RequestUri!.ToString());
	}

	[Fact]
	public async Task GetHealthAsync_BotNotRunning_ReturnsNoHealth()
	{
		var (client, _) = CreateClient(_ => throw new HttpRequestException("Connection refused"));

		var result = await client.GetHealthAsync();

		Assert.Null(result.Health);
		Assert.Contains("not reachable", result.Message);
	}
}
