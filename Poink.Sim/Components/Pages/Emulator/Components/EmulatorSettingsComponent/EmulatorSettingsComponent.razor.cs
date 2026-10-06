using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Settings dialog for the emulator. Edits a draft of the settings and hands the result back on Apply, so nothing
	/// changes until then.
	/// </summary>
	public partial class EmulatorSettingsComponent : ComponentBase
	{
		private const int MinBotCount = 1;
		private const int MaxBotCount = 10_000;
		private const double MinCapital = 1;
		private const double MaxCapital = 1_000_000_000;

		[Parameter, EditorRequired]
		public EmulatorSettings Settings { get; set; } = default!;

		[Parameter]
		public EventCallback<EmulatorSettings> OnApply { get; set; }

		[Parameter]
		public EventCallback OnClose { get; set; }

		private List<PopulationDraft> _populations = new();
		private double _leverage = 1;

		private string? ValidationMessage =>
			_leverage is < EmulatorSettings.MinLeverage or > EmulatorSettings.MaxLeverage
				? $"Leverage must be between {EmulatorSettings.MinLeverage:0}× and {EmulatorSettings.MaxLeverage:0}×"
			: _populations.FirstOrDefault(p => p.BotCount is < MinBotCount or > MaxBotCount) is { } invalidCount
				? $"{invalidCount.Name} needs between {MinBotCount:N0} and {MaxBotCount:N0} bots"
			: _populations.FirstOrDefault(p => p.StartingCapital is < MinCapital or > MaxCapital) is { } invalidCapital
				? $"{invalidCapital.Name} starting capital must be between {MinCapital:N0} and {MaxCapital:N0}"
			: null;

		private string ApplyTooltip =>
			Settings.RequiresReopen(Draft())
				? "Apply the settings -- the populations or their starting capital changed, so the market restarts with fresh bots, an empty book and a new warm-up"
				: "Apply the settings to the running market -- no restart";

		protected override void OnInitialized()
		{
			_populations = Settings.Populations.Select(p => new PopulationDraft(p.Name, p.BotCount, p.StartingPoints)).ToList();
			_leverage = Settings.Leverage;
		}

		private void SetLeverage(ChangeEventArgs e)
		{
			_leverage = double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var leverage) ? leverage : 0;
		}

		private EmulatorSettings Draft() =>
			Settings with
			{
				Leverage = _leverage,
				Populations = _populations.Select(p => new PopulationSettings(p.Name, p.BotCount, p.StartingCapital)).ToArray(),
			};

		private static void SetBotCount(PopulationDraft draft, object? value)
		{
			draft.BotCount = int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
		}

		private static void SetStartingCapital(PopulationDraft draft, object? value)
		{
			draft.StartingCapital = double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var capital) ? capital : 0;
		}

		private Task ApplyAsync() => OnApply.InvokeAsync(Draft());

		private sealed class PopulationDraft(string name, int botCount, double startingCapital)
		{
			public string Name { get; } = name;
			public int BotCount { get; set; } = botCount;
			public double StartingCapital { get; set; } = startingCapital;
		}
	}
}
