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
		private double _buyPercent;
		private double _sellPercent;
		private double _closePercent;
		private string? _error;

		private int ActiveBots => Bots.Count(bot => !bot.IsBust);

		private double HoldPercent => Math.Max(0, 100 - _buyPercent - _sellPercent - _closePercent);

		private string TableHeight =>
			$"{TableHeadPixels + Math.Clamp(Bots.Count, MinVisibleRows, MaxVisibleRows) * TableRowPixels}px";

		protected override void OnParametersSet()
		{
			if (!Equals(Group, _source))
			{
				_source = Group;
				_name = Group.Name;
				_botCount = Group.BotCount;
				_capital = Group.StartingPoints;
				_buyPercent = ToPercent(Group.BuyChance);
				_sellPercent = ToPercent(Group.SellChance);
				_closePercent = ToPercent(Group.CloseChance);
				_error = null;
			}
		}

		private async Task ChangeAsync(Action edit)
		{
			edit();
			var draft = new PopulationSettings(_name, _botCount, _capital, _buyPercent / 100, _sellPercent / 100, _closePercent / 100);
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
			: !draft.HasValidActionChances ? "Buy, sell and close must each be 0–100% and add up to 100% or less"
			: null;

		// Rounded so a chance read back from the card is the same double it was loaded from.
		private static double ToPercent(double chance) => Math.Round(chance * 100, 6);

		private static double ParseDouble(object? value) =>
			double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

		private static int ParseInt(object? value) =>
			int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
	}
}
