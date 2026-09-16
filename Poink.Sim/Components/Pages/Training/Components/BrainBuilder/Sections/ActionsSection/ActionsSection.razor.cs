using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class ActionsSection : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Parameter]
		public BrainBuilderState State { get; set; } = default!;

		[Parameter]
		public EventCallback OnChanged { get; set; }

		private Task NotifyChanged() => OnChanged.InvokeAsync();

		private const int TotalActionOptionCount = 7;

		private int SelectedActionCount =>
			DirectionCount
			+ (State.IncludeVolumeOutput ? 1 : 0)
			+ (State.IncludeStopLossOutput ? 1 : 0)
			+ (State.IncludeTakeProfitOutput ? 1 : 0);

		private bool AllActionsSelected
		{
			get => SelectedActionCount == TotalActionOptionCount;
			set
			{
				State.IncludeHold = value;
				State.IncludeEnterLong = value;
				State.IncludeEnterShort = value;
				State.IncludeClose = value;
				State.IncludeVolumeOutput = value;
				State.IncludeStopLossOutput = value;
				State.IncludeTakeProfitOutput = value;
			}
		}

		private IReadOnlyList<string> EnabledDirectionLabels
		{
			get
			{
				var labels = new List<string>();
				if (State.IncludeHold)
					labels.Add("Hold");
				if (State.IncludeEnterLong)
					labels.Add("Enter Long");
				if (State.IncludeEnterShort)
					labels.Add("Enter Short");
				if (State.IncludeClose)
					labels.Add("Close");
				return labels;
			}
		}

		private int DirectionCount => EnabledDirectionLabels.Count;

		private int VolumeOutputWidth =>
			BrainCatalog
				.CreateActionOutputHead(ActionOutputKind.Volume, ActionOutputEncodingKind.DiscretizedLevels)
				.OutputWidth;

		private int StopLossOutputWidth =>
			BrainCatalog
				.CreateActionOutputHead(ActionOutputKind.StopLoss, ActionOutputEncodingKind.DiscretizedLevels)
				.OutputWidth;

		private int TakeProfitOutputWidth =>
			BrainCatalog
				.CreateActionOutputHead(ActionOutputKind.TakeProfit, ActionOutputEncodingKind.DiscretizedLevels)
				.OutputWidth;

		private int TotalOutputWidth =>
			DirectionCount
			+ (State.IncludeVolumeOutput ? VolumeOutputWidth : 0)
			+ (State.IncludeStopLossOutput ? StopLossOutputWidth : 0)
			+ (State.IncludeTakeProfitOutput ? TakeProfitOutputWidth : 0);

		private string DecodingLabel =>
			State.DecisionDecoding switch
			{
				DecisionDecodingKind.HardMax => "hardmax",
				_ when State.DecisionTemperature != 1.0 => $"softmax @ T={State.DecisionTemperature:0.##}",
				_ => "softmax",
			};

		private sealed record MaskRuleRow(
			DirectionMaskRuleKind Kind,
			string Label,
			string Tooltip,
			System.Func<bool> Get,
			System.Action<bool> Set
		);

		// (Kind, Label, Tooltip, Get, Set) per rule -- same convention as DecisionMaskRuleRows in DecideSection.
		private IReadOnlyList<MaskRuleRow> MaskRuleRows =>
			new List<MaskRuleRow>
			{
				new(
					DirectionMaskRuleKind.PositionRequiredForClose,
					"Close Requires Position",
					"Close is illegal unless a position is currently open",
					() => State.MaskPositionRequiredForClose,
					v => State.MaskPositionRequiredForClose = v
				),
				new(
					DirectionMaskRuleKind.FlatRequiredForEntry,
					"Entry Requires Flat",
					"Enter Long / Enter Short are illegal while a position is already open",
					() => State.MaskFlatRequiredForEntry,
					v => State.MaskFlatRequiredForEntry = v
				),
				new(
					DirectionMaskRuleKind.LongRequiresUpSignal,
					"Long Requires Up Signal",
					"Enter Long is illegal unless the xGrid Rank Top % Up signal is active -- has no effect without the XGrid Signals encoder",
					() => State.MaskLongRequiresUpSignal,
					v => State.MaskLongRequiresUpSignal = v
				),
				new(
					DirectionMaskRuleKind.ShortRequiresDownSignal,
					"Short Requires Down Signal",
					"Enter Short is illegal unless the xGrid Rank Top % Down signal is active -- has no effect without the XGrid Signals encoder",
					() => State.MaskShortRequiresDownSignal,
					v => State.MaskShortRequiresDownSignal = v
				),
				new(
					DirectionMaskRuleKind.KeyLevelRequiredForClose,
					"Close Requires Key Level",
					"Close is illegal unless price is currently at POC, VAH, VAL, or VWAP -- has no effect without the XGrid Signals encoder",
					() => State.MaskKeyLevelRequiredForClose,
					v => State.MaskKeyLevelRequiredForClose = v
				),
			};

		private Task OnMaskRuleRowChanged(MaskRuleRow row, ChangeEventArgs e)
		{
			row.Set(e.Value is bool b && b);
			return NotifyChanged();
		}

		private const int TotalMaskRuleCount = 5;

		private int SelectedMaskRuleCount => MaskRuleRows.Count(r => r.Get());

		private bool AllMaskRulesSelected
		{
			get => SelectedMaskRuleCount == TotalMaskRuleCount;
			set
			{
				foreach (var row in MaskRuleRows)
					row.Set(value);
			}
		}
	}
}
