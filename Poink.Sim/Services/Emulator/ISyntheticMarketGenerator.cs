using System.Collections.Generic;

namespace Poink.Sim.Emulator;

/// <summary>
/// Produces the emulated market's price moves and resting book. Stands in for the bots and matching engine
/// until they exist, so the service can swap it out without the page changing.
/// </summary>
public interface ISyntheticMarketGenerator
{
	double NextPrice(double price, EmulatorSettings settings);

	OrderBook BuildBook(double price, EmulatorSettings settings);

	IReadOnlyList<PopulationLevel> BuildPopulation(OrderBook book, EmulatorSettings settings);
}
