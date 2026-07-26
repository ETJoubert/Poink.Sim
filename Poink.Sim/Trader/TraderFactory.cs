namespace Poink.Sim.Trader;

internal sealed class TraderFactory : ITraderFactory
{
    public ITrader Create(double startingCash, TraderSettings? settings = null) => new Trader(startingCash, settings);
}
