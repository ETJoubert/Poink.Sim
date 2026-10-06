using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// One row per bot population: how many are still trading, their combined account and their trading activity.
	/// </summary>
	public partial class EmulatorPopulationsComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<PopulationSummary> Populations { get; set; } = Array.Empty<PopulationSummary>();

		private static string Amount(double amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

		private static string SignClass(double amount) =>
			amount > 0 ? "emu-pop-positive"
			: amount < 0 ? "emu-pop-negative"
			: string.Empty;
	}
}
