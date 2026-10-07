namespace Poink.Sim.Services.OrderBot
{
	/// <summary>
	/// Binds the "OrderBot" config section: where the cTrader OrderBot bridge listens and whether a live
	/// account's trades are routed to it instead of the Open API (which the broker blocks for live trading).
	/// </summary>
	public sealed class OrderBotOptions
	{
		public const string SectionName = "OrderBot";

		public bool RouteLiveOrders { get; init; } = true;

		public string BaseUrl { get; init; } = "http://localhost:5099/";

		/// <summary>Shared secret matching the bot's CommandToken parameter. Supply it via appsettings.Secrets.json, never a tracked file.</summary>
		public string CommandToken { get; init; } = string.Empty;

		public int RequestTimeoutSeconds { get; init; } = 10;

		/// <summary>How long a command stays executable; the bot refuses one that reaches its main thread later than this.</summary>
		public int CommandLifetimeSeconds { get; init; } = 5;

		public int StatusPollSeconds { get; init; } = 5;
	}
}
