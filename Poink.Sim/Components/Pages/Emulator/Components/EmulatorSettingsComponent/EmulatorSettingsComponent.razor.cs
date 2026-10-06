using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Settings card for the emulator. Edits a draft of the settings and hands the result back on Apply, so nothing changes
	/// until then; while the draft is untouched it follows the settings in use.
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

		private EmulatorSettings? _source;
		private List<PopulationDraft> _populations = new();
		private double _leverage;
		private double _brokerFund;
		private int _orderRangeTicks;
		private double _activityMinPercent;
		private double _activityMaxPercent;

		private bool IsDirty => _source is not null && !SameChoices(_source, Draft());

		private string? ValidationMessage =>
			_leverage is < EmulatorSettings.MinLeverage or > EmulatorSettings.MaxLeverage or double.NaN
				? $"Leverage must be between {EmulatorSettings.MinLeverage:0}× and {EmulatorSettings.MaxLeverage:0}×"
			: _brokerFund is < EmulatorSettings.MinBrokerFund or > EmulatorSettings.MaxBrokerFund or double.NaN
				? $"Broker fund must be between {EmulatorSettings.MinBrokerFund:N0} and {EmulatorSettings.MaxBrokerFund:N0}"
			: _orderRangeTicks is < EmulatorSettings.MinOrderRangeTicks or > EmulatorSettings.MaxOrderRangeTicks
				? $"Order range must be between {EmulatorSettings.MinOrderRangeTicks:N0} and {EmulatorSettings.MaxOrderRangeTicks:N0} ticks"
			: _activityMinPercent is < 0 or > 100 or double.NaN || _activityMaxPercent is < 0 or > 100 or double.NaN
				? "Action frequency must be between 0% and 100%"
			: _activityMinPercent > _activityMaxPercent
				? "The lowest action frequency must not be above the highest"
			: _populations.FirstOrDefault(p => p.BotCount is < MinBotCount or > MaxBotCount) is { } invalidCount
				? $"{invalidCount.Name} needs between {MinBotCount:N0} and {MaxBotCount:N0} bots"
			: _populations.FirstOrDefault(p => p.StartingCapital is < MinCapital or > MaxCapital or double.NaN) is { } invalidCapital
				? $"{invalidCapital.Name} starting capital must be between {MinCapital:N0} and {MaxCapital:N0}"
			: null;

		private string ApplyTooltip =>
			Settings.RequiresReopen(Draft())
				? "Apply the settings -- the populations or their starting capital changed, so the market restarts with fresh bots, an empty book and a new warm-up"
				: "Apply the settings to the running market -- no restart";

		protected override void OnParametersSet()
		{
			if (!ReferenceEquals(Settings, _source) && !IsDirty)
			{
				LoadDraft(Settings);
			}
			_source = Settings;
		}

		private void LoadDraft(EmulatorSettings settings)
		{
			_populations = settings.Populations.Select(p => new PopulationDraft(p.Name, p.BotCount, p.StartingPoints)).ToList();
			_leverage = settings.Leverage;
			_brokerFund = settings.BrokerFund;
			_orderRangeTicks = settings.OrderRangeTicks;
			_activityMinPercent = settings.ActivityChanceMin * 100;
			_activityMaxPercent = settings.ActivityChanceMax * 100;
		}

		private void Revert() => LoadDraft(Settings);

		private EmulatorSettings Draft() =>
			Settings with
			{
				Leverage = _leverage,
				BrokerFund = _brokerFund,
				OrderRangeTicks = _orderRangeTicks,
				ActivityChanceMin = _activityMinPercent / 100,
				ActivityChanceMax = _activityMaxPercent / 100,
				Populations = _populations.Select(p => new PopulationSettings(p.Name, p.BotCount, p.StartingCapital)).ToArray(),
			};

		private static bool SameChoices(EmulatorSettings current, EmulatorSettings next) =>
			!current.RequiresReopen(next)
			&& current.Leverage == next.Leverage
			&& current.BrokerFund == next.BrokerFund
			&& current.OrderRangeTicks == next.OrderRangeTicks
			&& !current.ActivityRangeDiffers(next);

		private static double ParseDouble(object? value) =>
			double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

		private static int ParseInt(object? value) =>
			int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

		private Task ApplyAsync() => OnApply.InvokeAsync(Draft());

		private sealed class PopulationDraft(string name, int botCount, double startingCapital)
		{
			public string Name { get; } = name;
			public int BotCount { get; set; } = botCount;
			public double StartingCapital { get; set; } = startingCapital;
		}
	}
}
