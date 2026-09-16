using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Analysis;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Analize
{
	public partial class AnalizePage : ComponentBase, IDisposable
	{
		[Inject]
		private ITimeAboveBelowAnalysisService AnalysisService { get; set; } = default!;

		private bool _leftPanelCollapsed;
		private bool _rightPanelCollapsed;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;

		private double _windowMinutes = 60;
		private string? _startError;

		protected override void OnInitialized() => AnalysisService.OnChanged += HandleServiceChanged;

		public void Dispose() => AnalysisService.OnChanged -= HandleServiceChanged;

		private void HandleServiceChanged() => InvokeAsync(StateHasChanged);

		private void RunAnalysis()
		{
			_startError = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_startError = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_startError = "To must be after From";
				return;
			}
			if (_windowMinutes <= 0)
			{
				_startError = "window must be greater than 0";
				return;
			}

			if (!AnalysisService.TryStart(new TimeAboveBelowRequest(_table, _from, _to, _windowMinutes)))
				_startError = "analysis is already running";
		}

		private IReadOnlyList<HistogramBar> ChartBins =>
			AnalysisService.Result is { } result
				? result.Bins.Select(b => new HistogramBar(b.Low, b.High, b.Count)).ToList()
				: Array.Empty<HistogramBar>();

		// CSS rejects a comma decimal separator, so this can't use the current (possibly non-US) culture.
		private string ProgressPercentText => AnalysisService.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture);
	}
}
