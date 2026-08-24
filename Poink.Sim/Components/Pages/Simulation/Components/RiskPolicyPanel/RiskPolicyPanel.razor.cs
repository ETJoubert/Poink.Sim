using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Simulation
{
	public partial class RiskPolicyPanel : ComponentBase
	{
		[Inject]
		private ISimulationEngine Service { get; set; } = default!;

		[Parameter]
		public IReadOnlyList<RiskPolicySnapshot> Policies { get; set; } = [];

		[Parameter]
		public bool Locked { get; set; }

		[Parameter]
		public IReadOnlyList<RiskPolicyStatus>? Statuses { get; set; }

		[Parameter]
		public RiskPolicyDenial? LastDenial { get; set; }

		private string? StatusFor(string policyName)
		{
			if (Statuses is null)
				return null;
			foreach (var status in Statuses)
				if (status.Name == policyName)
					return status.Status;
			return null;
		}

		private bool DeniedBy(string policyName) => LastDenial?.PolicyName == policyName;

		private void ToggleCard(RiskPolicySnapshot policy)
		{
			if (Locked)
				return;
			Service.SetRiskPolicyEnabled(policy.Name, !policy.Enabled);
		}

		private void OnCardKey(KeyboardEventArgs args, RiskPolicySnapshot policy)
		{
			if (args.Key is "Enter" or " " or "Spacebar")
				ToggleCard(policy);
		}

		private void MoveRiskPolicy(string policyName, int delta) => Service.MoveRiskPolicy(policyName, delta);

		private void SetParameter(string policyName, string key, ChangeEventArgs args)
		{
			if (double.TryParse(args.Value?.ToString(), out var value))
				Service.SetRiskPolicyParameter(policyName, key, value);
		}
	}
}
