using System.Collections.Generic;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskPolicyService
{
	IReadOnlyList<RiskPolicyInfo> AvailablePolicies { get; }

	ITrader Wrap(ITrader core, IReadOnlyList<string>? policyNames, RiskPolicyToggles? toggles = null);

	RiskPolicyToggles? CreateToggles(IReadOnlyList<string>? policyNames, bool locked);
}
