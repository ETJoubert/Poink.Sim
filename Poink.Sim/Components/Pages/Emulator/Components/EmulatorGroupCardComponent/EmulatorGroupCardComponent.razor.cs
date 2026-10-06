using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// One bot group as a card: its bots' accounts on the left and its settings on the right. A valid edit is handed back
	/// as soon as a field changes; an invalid one stays on the card with the reason until it is fixed.
	/// </summary>
	public partial class EmulatorGroupCardComponent : ComponentBase
	{
		private const int MinBotCount = 1;
		private const int MaxBotCount = 10_000;
		private const double MinCapital = 1;
		private const double MaxCapital = 1_000_000_000;
		private const int TableRowPixels = 17;
		private const int TableHeadPixels = 24;
		private const int MaxVisibleRows = 12;
		private const int MinVisibleRows = 6;
		private const double TableHeightScale = 1.1;
		private const double CapitalStep = 1_000;

		[Parameter, EditorRequired]
		public PopulationSettings Group { get; set; } = default!;

		[Parameter, EditorRequired]
		public IReadOnlyList<BotSummary> Bots { get; set; } = Array.Empty<BotSummary>();

		[Parameter, EditorRequired]
		public string Color { get; set; } = string.Empty;

		[Parameter]
		public IReadOnlyCollection<string> OtherGroupNames { get; set; } = Array.Empty<string>();

		[Parameter]
		public bool CanRemove { get; set; }

		[Parameter]
		public EventCallback<PopulationSettings> OnChange { get; set; }

		[Parameter]
		public EventCallback OnRemove { get; set; }

		[Parameter]
		public EventCallback OnReplenish { get; set; }

		[Parameter]
		public EventCallback<int> OnReplenishBot { get; set; }

		private PopulationSettings? _source;
		private string _name = string.Empty;
		private int _botCount;
		private double _capital;
		private double _buyWeight;
		private double _sellWeight;
		private double _closeWeight;
		private double _holdWeight;
		private string? _error;
		private readonly IReadOnlyList<WeightField> _weightFields;

		public EmulatorGroupCardComponent()
		{
			_weightFields =
			[
				new("Buy", "When a bot acts: weight for placing a buy order", () => _buyWeight, w => _buyWeight = w),
				new("Sell", "When a bot acts: weight for placing a sell order", () => _sellWeight, w => _sellWeight = w),
				new("Close", "When a bot acts: weight for closing its whole position at market", () => _closeWeight, w => _closeWeight = w),
				new("Hold", "When a bot acts: weight for doing nothing this step", () => _holdWeight, w => _holdWeight = w),
			];
		}

		private int ActiveBots => Bots.Count(bot => !bot.IsBust);

		private double TotalWeight => _buyWeight + _sellWeight + _closeWeight + _holdWeight;

		private static PopulationSettings DefaultWeights { get; } = new(string.Empty, 0, 0);

		private bool WeightsAreDefault =>
			(_buyWeight, _sellWeight, _closeWeight, _holdWeight)
			== (DefaultWeights.BuyWeight, DefaultWeights.SellWeight, DefaultWeights.CloseWeight, DefaultWeights.HoldWeight);

		private Task ResetWeightsAsync() =>
			ChangeAsync(() =>
			{
				_buyWeight = DefaultWeights.BuyWeight;
				_sellWeight = DefaultWeights.SellWeight;
				_closeWeight = DefaultWeights.CloseWeight;
				_holdWeight = DefaultWeights.HoldWeight;
			});

		private string TableHeight =>
			$"{Math.Round((TableHeadPixels + Math.Clamp(Bots.Count, MinVisibleRows, MaxVisibleRows) * TableRowPixels) * TableHeightScale)}px";

		protected override void OnParametersSet()
		{
			if (!Equals(Group, _source))
			{
				_source = Group;
				_name = Group.Name;
				_botCount = Group.BotCount;
				_capital = Group.StartingPoints;
				_buyWeight = Group.BuyWeight;
				_sellWeight = Group.SellWeight;
				_closeWeight = Group.CloseWeight;
				_holdWeight = Group.HoldWeight;
				_error = null;
			}
		}

		private async Task ChangeAsync(Action edit)
		{
			edit();
			var draft = new PopulationSettings(_name, _botCount, _capital, _buyWeight, _sellWeight, _closeWeight, _holdWeight);
			_error = Validate(draft);
			if (_error is null && draft != Group)
			{
				await OnChange.InvokeAsync(draft);
			}
		}

		private string? Validate(PopulationSettings draft) =>
			string.IsNullOrWhiteSpace(draft.Name) ? "The group needs a name"
			: OtherGroupNames.Contains(draft.Name) ? $"Another group is already named {draft.Name}"
			: draft.BotCount is < MinBotCount or > MaxBotCount ? $"Bots must be between {MinBotCount:N0} and {MaxBotCount:N0}"
			: draft.StartingPoints is < MinCapital or > MaxCapital or double.NaN ? $"Capital must be between {MinCapital:N0} and {MaxCapital:N0}"
			: !draft.HasValidWeights ? "Weights must be zero or more"
			: null;

		private string Share(double weight) =>
			TotalWeight > 0 && double.IsFinite(TotalWeight)
				? (weight / TotalWeight).ToString("P0", CultureInfo.InvariantCulture)
				: "–";

		private static int ToBotCount(double value) => double.IsFinite(value) && value == Math.Floor(value) ? (int)Math.Clamp(value, 0, int.MaxValue) : 0;

		private sealed record WeightField(string Label, string Tooltip, Func<double> Get, Action<double> Set);
	}
}
