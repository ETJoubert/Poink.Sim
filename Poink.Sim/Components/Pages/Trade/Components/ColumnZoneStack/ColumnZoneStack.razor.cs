using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// One <see cref="ColumnZoneSlider"/> per XGrid column from the latest (n) back to n-k, with k and the zone share
	/// they all use adjustable and kept per browser.
	/// </summary>
	public partial class ColumnZoneStack : ComponentBase
	{
		private const string ColumnsBackStorageKey = "trade.quickConsole.columnsBack";

		private const string ZonePercentStorageKey = "trade.quickConsole.columnZonePercent";

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		[Inject]
		private ILogger<ColumnZoneStack> Logger { get; set; } = null!;

		/// <summary>The charted symbol's XGrid; null while the chart has none.</summary>
		[Parameter]
		public XGridResult? Grid { get; set; }

		private int _columnsBack = ColumnZoneHelper.DefaultColumnsBack;

		private double _zonePercent = ColumnZoneHelper.DefaultZonePercent;

		private IReadOnlyList<ColumnZoneRange?> Ranges => ColumnZoneHelper.RecentColumns(Grid, _columnsBack);

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;

			if (await LocalStorage.GetItemAsync<int?>(ColumnsBackStorageKey) is { } storedColumnsBack)
				_columnsBack = ColumnZoneHelper.ClampColumnsBack(storedColumnsBack);
			if (await LocalStorage.GetItemAsync<double?>(ZonePercentStorageKey) is { } storedZonePercent)
				_zonePercent = ColumnZoneHelper.ClampZonePercent(storedZonePercent);

			Logger.LogInformation(
				"Column zone sliders loaded for columns n to n-{ColumnsBack} at a {ZonePercent}% zone share",
				_columnsBack,
				_zonePercent
			);
			StateHasChanged();
		}

		private async Task SetColumnsBackAsync(ChangeEventArgs args)
		{
			var requested = int.TryParse(args.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: _columnsBack;
			_columnsBack = ColumnZoneHelper.ClampColumnsBack(requested);
			Logger.LogInformation("Column zone sliders set to columns n to n-{ColumnsBack}", _columnsBack);
			await LocalStorage.SetItemAsync(ColumnsBackStorageKey, _columnsBack);
		}

		private async Task SetZonePercentAsync(ChangeEventArgs args)
		{
			var requested = double.TryParse(args.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: _zonePercent;
			_zonePercent = ColumnZoneHelper.ClampZonePercent(requested);
			Logger.LogInformation("Column zone share set to {ZonePercent}%", _zonePercent);
			await LocalStorage.SetItemAsync(ZonePercentStorageKey, _zonePercent);
		}

		private static string FormatPercent(double percent) => percent.ToString("0", CultureInfo.InvariantCulture);
	}
}
