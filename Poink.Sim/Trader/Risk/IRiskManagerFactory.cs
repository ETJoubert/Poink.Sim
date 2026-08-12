
using System.Collections.Generic;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskManagerFactory
{
    ITrader Wrap(ITrader core, IReadOnlyList<string>? policyNames, RiskPolicyToggles? toggles = null);
}
