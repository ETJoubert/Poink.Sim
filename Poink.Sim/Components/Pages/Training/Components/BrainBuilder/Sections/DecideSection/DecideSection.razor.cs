using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class DecideSection : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Parameter]
		public BrainBuilderState State { get; set; } = default!;

		[Parameter]
		public EventCallback OnChanged { get; set; }

		private Task NotifyChanged() => OnChanged.InvokeAsync();

		private sealed record DecisionMaskRuleRow(
			DecisionMaskRuleKind Kind,
			string Label,
			string Tooltip,
			System.Func<bool> Get,
			System.Action<bool> Set
		);

		// (Kind, Label, Tooltip, Get, Set) per rule -- same convention as MaskRuleRows in ActionsSection.
		private IReadOnlyList<DecisionMaskRuleRow> DecisionMaskRuleRows =>
			new List<DecisionMaskRuleRow>
			{
				new(
					DecisionMaskRuleKind.PriceBelowPoc,
					"Price Below POC",
					"A decision only fires when price is below the trailing window's Point of Control -- has no effect without the XGrid Signals encoder",
					() => State.MaskDecisionPriceBelowPoc,
					v => State.MaskDecisionPriceBelowPoc = v
				),
			};

		private Task OnDecisionMaskRuleRowChanged(DecisionMaskRuleRow row, ChangeEventArgs e)
		{
			row.Set(e.Value is bool b && b);
			return NotifyChanged();
		}

		private const int TotalDecisionMaskRuleCount = 1;

		private int SelectedDecisionMaskRuleCount => DecisionMaskRuleRows.Count(r => r.Get());

		private bool AllDecisionMaskRulesSelected
		{
			get => SelectedDecisionMaskRuleCount == TotalDecisionMaskRuleCount;
			set
			{
				foreach (var row in DecisionMaskRuleRows)
					row.Set(value);
			}
		}
	}
}
