namespace Poink.Sim.Trader;

using Poink.Sim.Trader.Risk;

internal sealed class TraderFactory : ITraderFactory
{
    private readonly IRiskManagerFactory _riskManagerFactory;

    public TraderFactory(IRiskManagerFactory riskManagerFactory) => _riskManagerFactory = riskManagerFactory;

    public ITrader Create(double startingCash, TraderSettings? settings = null)
    {
        var effectiveSettings = settings ?? new TraderSettings();
        var trader = new Trader(startingCash, effectiveSettings);
        return _riskManagerFactory.Wrap(trader, effectiveSettings.EffectiveRiskPolicyNames, effectiveSettings.RiskToggles);
    }
}
