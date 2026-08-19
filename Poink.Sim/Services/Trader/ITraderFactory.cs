
namespace Poink.Sim.Trader;

internal interface ITraderFactory
{
    ITrader Create(double startingCash, TraderSettings? settings = null);
}
