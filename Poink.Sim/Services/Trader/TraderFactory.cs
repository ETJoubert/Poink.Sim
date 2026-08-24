namespace Poink.Sim.Trader;

using Poink.Sim.Trader.Risk;

/// <summary>
/// Builds a <see cref="Trader"/> for one session and wraps it with risk management
/// according to the session's settings.
/// </summary>
internal sealed class TraderFactory : ITraderFactory
{
	private readonly IRiskPolicyService _riskPolicyService;

	public TraderFactory(IRiskPolicyService riskPolicyService) => _riskPolicyService = riskPolicyService;

	public ITrader Create(double startingCash, TraderSettings? settings = null)
	{
		var effectiveSettings = settings ?? new TraderSettings();
		var trader = new Trader(startingCash, effectiveSettings);
		return _riskPolicyService.Wrap(
			trader,
			effectiveSettings.EffectiveRiskPolicyNames,
			effectiveSettings.RiskToggles
		);
	}
}
