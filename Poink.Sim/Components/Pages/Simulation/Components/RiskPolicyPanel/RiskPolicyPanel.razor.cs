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
        [Parameter] public IReadOnlyList<RiskPolicyStatus>? Statuses { get; set; }

        private string? StatusFor(string policyName)
        {
            if (Statuses is null)
                return null;
            foreach (var status in Statuses)
                if (status.Name == policyName)
                    return status.Status;
            return null;
        }

        private void ToggleRiskPolicy(string policyName, bool enabled) => Service.SetRiskPolicyEnabled(policyName, enabled);

        private void MoveRiskPolicy(string policyName, int delta) => Service.MoveRiskPolicy(policyName, delta);
    }
}
