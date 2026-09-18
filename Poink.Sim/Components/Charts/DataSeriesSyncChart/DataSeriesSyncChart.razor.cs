using System;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.DataSeriesSync;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Left-to-right day squares from the tick store's latest coverage to today, via
	/// <see cref="IDataSeriesSyncService"/> -- the leftmost square is the latest day already in
	/// the database, the rightmost is today, and everything between is either found in the
	/// registry's saved import source directory (ready to import) or unverified (would need the API).
	/// </summary>
	public partial class DataSeriesSyncChart : ComponentBase
	{
		[Inject]
		private IDataSeriesSyncService SyncService { get; set; } = default!;

		[Parameter, EditorRequired]
		public string SymbolName { get; set; } = "";

		/// <summary>Bump to force a reload after something outside this component's own parameters changes its data, e.g. an import completing.</summary>
		[Parameter]
		public int RefreshToken { get; set; }

		private static readonly DataSeriesSyncDay[] NoDays = Array.Empty<DataSeriesSyncDay>();

		private DataSeriesSyncRange _range = new("", false, null, DateOnly.FromDateTime(DateTime.UtcNow), null, NoDays);
		private (string SymbolName, int RefreshToken)? _loadedFor;

		protected override void OnParametersSet()
		{
			var key = (SymbolName, RefreshToken);
			if (_loadedFor == key)
				return;

			_loadedFor = key;
			_range = SyncService.GetSyncRange(SymbolName);
		}

		private static string StatusClass(DataSeriesSyncDayStatus status) => status switch
		{
			DataSeriesSyncDayStatus.InDatabase => "in-db",
			DataSeriesSyncDayStatus.AvailableInSource => "in-source",
			DataSeriesSyncDayStatus.CurrentDay => "current",
			_ => "available",
		};

		private static string DayTooltip(DataSeriesSyncDay day) => day.Status switch
		{
			DataSeriesSyncDayStatus.InDatabase => $"{day.Date:yyyy-MM-dd} -- latest day already in the database",
			DataSeriesSyncDayStatus.AvailableInSource => $"{day.Date:yyyy-MM-dd} -- found in the saved source directory, ready to import",
			DataSeriesSyncDayStatus.CurrentDay => $"{day.Date:yyyy-MM-dd} -- current day",
			_ => $"{day.Date:yyyy-MM-dd} -- not in the saved source; would need a live API fetch",
		};

		private string RootTooltip
		{
			get
			{
				if (!_range.HasData)
					return $"No tick data found in the database for {SymbolName}";

				var inSource = _range.Days.Count(d => d.Status == DataSeriesSyncDayStatus.AvailableInSource);
				var unverified = _range.Days.Count(d => d.Status == DataSeriesSyncDayStatus.AvailableToLoad);
				var sourceNote = _range.SourceDirectory is { } dir ? $", source: {dir}" : ", no saved source directory";
				return $"{SymbolName}: database covers through {_range.LatestDatabaseDate:yyyy-MM-dd} -- {inSource} day(s) ready to import, {unverified} unverified{sourceNote}";
			}
		}
	}
}
