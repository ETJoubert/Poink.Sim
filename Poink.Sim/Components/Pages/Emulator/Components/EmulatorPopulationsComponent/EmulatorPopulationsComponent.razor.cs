using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// One row per bot group, marked with the group's colour: how many are still trading, their combined account and
	/// their trading activity.
	/// </summary>
	public partial class EmulatorPopulationsComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationSummary> Populations { get; set; } = Array.Empty<PopulationSummary>();

		/// <summary>The groups in their settings order, which fixes each group's colour.</summary>
		[Parameter]
		public IReadOnlyList<PopulationSettings> Groups { get; set; } = Array.Empty<PopulationSettings>();

		private string ColorOf(string group)
		{
			var index = Groups.Select(g => g.Name).ToList().IndexOf(group);
			return index < 0 ? "var(--color-secondary)" : EmulatorGroupColorHelper.ColorVar(index);
		}

		private static string Amount(double amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

		private static string SignClass(double amount) =>
			amount > 0 ? "emu-pop-positive"
			: amount < 0 ? "emu-pop-negative"
			: string.Empty;
	}
}
