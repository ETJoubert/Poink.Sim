using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Shared.PolicySelector
{
	public partial class PolicySelector : ComponentBase
	{
		[Parameter]
		public IReadOnlyList<RiskPolicyInfo> Available { get; set; } = [];

		[Parameter]
		public List<string> Selected { get; set; } = [];

		[Parameter]
		public bool Disabled { get; set; }

		[Parameter]
		public EventCallback OnChanged { get; set; }

		[Parameter]
		public RenderFragment? Actions { get; set; }

		private bool _open;

		private void ToggleOpen() => _open = !_open;

		private IEnumerable<RiskPolicyInfo> OrderedRows =>
			Selected
				.Select(name => Available.FirstOrDefault(policy => policy.Name == name))
				.Where(policy => policy.Name is not null)
				.Concat(Available.Where(policy => !Selected.Contains(policy.Name)));

		private int ExecutionOrder(string name) => Selected.IndexOf(name);

		private async Task SetEnabled(string name, bool enabled)
		{
			if (Disabled)
				return;
			if (enabled)
			{
				if (!Selected.Contains(name))
					Selected.Add(name);
			}
			else
			{
				Selected.Remove(name);
			}
			await OnChanged.InvokeAsync();
		}

		private async Task OnCardKey(KeyboardEventArgs args, string name, bool enabled)
		{
			if (args.Key is "Enter" or " " or "Spacebar")
				await SetEnabled(name, !enabled);
		}

		private async Task MoveEarlier(string name)
		{
			int index = Selected.IndexOf(name);
			if (index <= 0)
				return;
			(Selected[index - 1], Selected[index]) = (Selected[index], Selected[index - 1]);
			await OnChanged.InvokeAsync();
		}

		private async Task MoveLater(string name)
		{
			int index = Selected.IndexOf(name);
			if (index < 0 || index >= Selected.Count - 1)
				return;
			(Selected[index + 1], Selected[index]) = (Selected[index], Selected[index + 1]);
			await OnChanged.InvokeAsync();
		}
	}
}
