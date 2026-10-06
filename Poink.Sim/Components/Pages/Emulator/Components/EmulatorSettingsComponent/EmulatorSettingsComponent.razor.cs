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

		[Parameter, EditorRequired]
		public EmulatorSettings Settings { get; set; } = default!;

		[Parameter]
		public EventCallback<EmulatorSettings> OnApply { get; set; }

		[Parameter]
		public EventCallback OnClose { get; set; }

		private List<PopulationDraft> _populations = new();

		private string? ValidationMessage =>
			_populations.FirstOrDefault(p => p.BotCount is < MinBotCount or > MaxBotCount) is { } invalid
				? $"{invalid.Name} needs between {MinBotCount:N0} and {MaxBotCount:N0} bots"
				: null;

		protected override void OnInitialized()
		{
			_populations = Settings.Populations.Select(p => new PopulationDraft(p.Name, p.BotCount, p.StartingPoints)).ToList();
		}

		private static void SetBotCount(PopulationDraft draft, object? value)
		{
			draft.BotCount = int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
		}

		private Task ApplyAsync() =>
			OnApply.InvokeAsync(
				Settings with
				{
					Populations = _populations.Select(p => new PopulationSettings(p.Name, p.BotCount, p.StartingPoints)).ToArray(),
				}
			);

		private sealed class PopulationDraft(string name, int botCount, double startingPoints)
		{
			public string Name { get; } = name;
			public int BotCount { get; set; } = botCount;
			public double StartingPoints { get; } = startingPoints;
		}
	}
}
