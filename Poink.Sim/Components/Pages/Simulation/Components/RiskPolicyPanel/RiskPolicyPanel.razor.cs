using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Simulation
{
    public partial class RiskPolicyPanel : ComponentBase
    {
        [Inject] private ISimulationEngine Service { get; set; } = default!;

        [Parameter] public IReadOnlyList<RiskPolicySnapshot> Policies { get; set; } = [];
        [Parameter] public bool Locked { get; set; }

        private void ToggleRiskPolicy(string policyName, bool enabled) => Service.SetRiskPolicyEnabled(policyName, enabled);
    }
}
