using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>HTTP transport to the cTrader OrderBot bridge. Never throws for transport failures; they come back as an unsuccessful result.</summary>
	internal interface IOrderBotClient
	{
		Task<OrderBotCommandResult> SendAsync(OrderBotCommand command, CancellationToken cancellationToken = default);

		Task<OrderBotHealthResult> GetHealthAsync(CancellationToken cancellationToken = default);
	}
}
