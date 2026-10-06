using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// Settings column for the emulator: the broker's terms and how bots trade, all applied to the running market. Edits a
	/// draft and hands it back on Apply; while the draft is untouched it follows the settings in use. The bot groups are
	/// edited in the Bots column.
	/// </summary>
	public partial class EmulatorSettingsComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public EmulatorSettings Settings { get; set; } = default!;

		[Parameter]
		public EventCallback<EmulatorSettings> OnApply { get; set; }

		private EmulatorSettings? _source;
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
			: null;

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
			_leverage = settings.Leverage;
			_brokerFund = settings.BrokerFund;
			_orderRangeTicks = settings.OrderRangeTicks;
			_activityMinPercent = Math.Round(settings.ActivityChanceMin * 100, 6);
			_activityMaxPercent = Math.Round(settings.ActivityChanceMax * 100, 6);
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
			};

		private static bool SameChoices(EmulatorSettings current, EmulatorSettings next) =>
			current.Leverage == next.Leverage
			&& current.BrokerFund == next.BrokerFund
			&& current.OrderRangeTicks == next.OrderRangeTicks
			&& !current.ActivityRangeDiffers(next);

		private static double ParseDouble(object? value) =>
			double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

		private static int ParseInt(object? value) =>
			int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

		private Task ApplyAsync() => OnApply.InvokeAsync(Draft());
	}
}
