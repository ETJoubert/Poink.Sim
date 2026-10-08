using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// A number with − and + buttons that step it by <see cref="Step"/> within <see cref="Min"/>–<see cref="Max"/>; it can
	/// still be typed. Every step or edit is handed back at once through <see cref="OnChange"/>.
	/// </summary>
	public partial class EmulatorStepperComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public double Value { get; set; }

		[Parameter]
		public double Step { get; set; } = 1;

		[Parameter]
		public double Min { get; set; }

		[Parameter]
		public double Max { get; set; } = double.MaxValue;

		[Parameter]
		public string Tooltip { get; set; } = string.Empty;

		[Parameter]
		public bool Disabled { get; set; }

		[Parameter]
		public EventCallback<double> OnChange { get; set; }

		/// <summary>The value one step up or down, kept within bounds; an unreadable value steps from the minimum.</summary>
		internal static double Stepped(double value, int direction, double step, double min, double max)
		{
			var start = double.IsFinite(value) ? value : min;
			return Math.Clamp(Math.Round(start + direction * step, 6), min, max);
		}

		private Task StepAsync(int direction) => OnChange.InvokeAsync(Stepped(Value, direction, Step, Min, Max));

		private static double Parse(object? value) =>
			double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
	}
}
