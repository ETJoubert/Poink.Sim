using System;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.DataSeriesSync;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Left-to-right day squares from the tick store's latest coverage to today, via
	/// <see cref="IDataSeriesSyncService"/> -- the leftmost square is the latest day already in
	/// the database, the rightmost is today, and everything between is available to load from cTrader.
	/// </summary>
	public partial class DataSeriesSyncChart : ComponentBase
	{
		[Inject]
		private IDataSeriesSyncService SyncService { get; set; } = default!;

		[Parameter, EditorRequired]
		public string SymbolName { get; set; } = "";

		private static readonly DataSeriesSyncDay[] NoDays = Array.Empty<DataSeriesSyncDay>();

		private DataSeriesSyncRange _range = new("", false, null, DateOnly.FromDateTime(DateTime.UtcNow), NoDays);
		private string? _loadedFor;

		protected override void OnParametersSet()
		{
			if (_loadedFor == SymbolName)
				return;

			_loadedFor = SymbolName;
			_range = SyncService.GetSyncRange(SymbolName);
		}

		private static string StatusClass(DataSeriesSyncDayStatus status) => status switch
		{
			DataSeriesSyncDayStatus.InDatabase => "in-db",
			DataSeriesSyncDayStatus.CurrentDay => "current",
			_ => "available",
		};

		private static string DayTooltip(DataSeriesSyncDay day) => day.Status switch
		{
			DataSeriesSyncDayStatus.InDatabase => $"{day.Date:yyyy-MM-dd} -- latest day already in the database",
			DataSeriesSyncDayStatus.CurrentDay => $"{day.Date:yyyy-MM-dd} -- current day",
			_ => $"{day.Date:yyyy-MM-dd} -- available to load from cTrader",
		};

		private string RootTooltip
		{
			get
			{
				if (!_range.HasData)
					return $"No tick data found in the database for {SymbolName}";

				var loadable = _range.Days.Count(d => d.Status == DataSeriesSyncDayStatus.AvailableToLoad);
				return $"{SymbolName}: database covers through {_range.LatestDatabaseDate:yyyy-MM-dd}, {loadable} day(s) available to load up to today";
			}
		}
	}
}
