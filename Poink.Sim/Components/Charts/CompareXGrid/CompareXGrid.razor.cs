using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Several symbols' footprints on one grid of % rows by time columns (see <see cref="CompareXGridResult"/>), coloured
	/// per <see cref="Mode"/>. The host computes <see cref="Result"/> and recomputes it when the bound row size or column
	/// count changes; mode and difference symbol are display-only. Draws via its own compareXGrid.js module.
	/// </summary>
	public partial class CompareXGrid : ComponentBase, IAsyncDisposable
	{
		public static readonly IReadOnlyList<double> RowPercentOptions = [0.01, 0.02, 0.05, 0.1, 0.2, 0.5];

		public static readonly IReadOnlyList<int> ColCountOptions = [10, 20, 40, 80];

		/// <summary>Shared by the top and low cell outlines; 0 turns them off.</summary>
		public static readonly IReadOnlyList<int> MarkedCellsPercentOptions = [0, 5, 10, 20, 30];

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = default!;

		/// <summary>Null shows <see cref="NoDataMessage"/>.</summary>
		[Parameter]
		public CompareXGridResult? Result { get; set; }

		[Parameter]
		public string NoDataMessage { get; set; } = "No ticks to grid.";

		[Parameter]
		public CompareXGridMode Mode { get; set; }

		[Parameter]
		public EventCallback<CompareXGridMode> ModeChanged { get; set; }

		/// <summary>The symbol the first one is compared against in <see cref="CompareXGridMode.Difference"/>; defaults to the second.</summary>
		[Parameter]
		public string? DifferenceLabel { get; set; }

		[Parameter]
		public EventCallback<string?> DifferenceLabelChanged { get; set; }

		[Parameter]
		public double RowPercent { get; set; } = 0.05;

		[Parameter]
		public EventCallback<double> RowPercentChanged { get; set; }

		[Parameter]
		public int ColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> ColCountChanged { get; set; }

		/// <summary>Draws a per-row tick profile beside the grid for every shown symbol.</summary>
		[Parameter]
		public bool ShowProfile { get; set; }

		[Parameter]
		public EventCallback<bool> ShowProfileChanged { get; set; }

		/// <summary>Outlines each shown symbol's busiest this-many-percent of its non-empty cells; 0 turns it off.</summary>
		[Parameter]
		public int TopCellsPercent { get; set; }

		[Parameter]
		public EventCallback<int> TopCellsPercentChanged { get; set; }

		/// <summary>Dash-outlines each shown symbol's quietest this-many-percent of its non-empty cells; 0 turns it off.</summary>
		[Parameter]
		public int LowCellsPercent { get; set; }

		[Parameter]
		public EventCallback<int> LowCellsPercentChanged { get; set; }

		private ElementReference _canvasRef;
		private IJSObjectReference? _module;
		private (CompareXGridResult Result, CompareXGridMode Mode, int DifferenceIndex, bool ShowProfile, int TopCellsPercent, int LowCellsPercent)? _rendered;
		private bool _disposed;

		private IReadOnlyList<CompareXGridSymbol> Symbols => Result?.Symbols ?? [];

		private string? FirstLabel => Symbols.Count > 0 ? Symbols[0].Label : null;

		private IEnumerable<string> DifferenceCandidates => Symbols.Skip(1).Select(s => s.Label);

		private int DifferenceIndex
		{
			get
			{
				if (Symbols.Count < 2)
					return -1;
				var index = Symbols.Select(s => s.Label).ToList().IndexOf(DifferenceLabel ?? "");
				return index > 0 ? index : 1;
			}
		}

		private string? EffectiveDifferenceLabel => DifferenceIndex > 0 ? Symbols[DifferenceIndex].Label : null;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (Result is not { } result)
			{
				_rendered = null;
				return;
			}

			try
			{
				_module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/compareXGrid.js");
				if (_disposed)
					return;

				var state = (result, Mode, DifferenceIndex, ShowProfile, TopCellsPercent, LowCellsPercent);
				if (_rendered is { } rendered
					&& ReferenceEquals(rendered.Result, result)
					&& (rendered.Mode, rendered.DifferenceIndex, rendered.ShowProfile, rendered.TopCellsPercent, rendered.LowCellsPercent)
						== (state.Mode, state.DifferenceIndex, state.ShowProfile, state.TopCellsPercent, state.LowCellsPercent))
					return;
				_rendered = state;

				await _module.InvokeVoidAsync("render", _canvasRef, RenderOptions(result));
			}
			catch (JSDisconnectedException) { }
			catch (ObjectDisposedException) { }
		}

		private object RenderOptions(CompareXGridResult result) =>
			new
			{
				rowCount = result.RowCount,
				colCount = result.ColCount,
				topPercent = result.TopPercent,
				rowPercent = result.RowPercent,
				columnStarts = result.ColumnStarts.Select(ToUnixSeconds),
				end = ToUnixSeconds(result.End),
				mode = Mode == CompareXGridMode.Difference ? "difference" : "dominant",
				differenceIndex = DifferenceIndex,
				showProfile = ShowProfile,
				topCellsPercent = TopCellsPercent,
				lowCellsPercent = LowCellsPercent,
				symbols = result.Symbols.Select(s => new { label = s.Label, colorVar = s.ColorVar, counts = s.Counts, max = s.MaxCount }),
			};

		private static long ToUnixSeconds(DateTime time) => new DateTimeOffset(time, TimeSpan.Zero).ToUnixTimeSeconds();

		private Task SetMode(CompareXGridMode mode)
		{
			Mode = mode;
			return ModeChanged.InvokeAsync(mode);
		}

		private Task OnDifferenceSelected(ChangeEventArgs e)
		{
			DifferenceLabel = e.Value?.ToString();
			return DifferenceLabelChanged.InvokeAsync(DifferenceLabel);
		}

		private Task OnRowPercentSelected(ChangeEventArgs e)
		{
			if (!double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rowPercent))
				return Task.CompletedTask;
			RowPercent = rowPercent;
			return RowPercentChanged.InvokeAsync(rowPercent);
		}

		private Task OnColCountSelected(ChangeEventArgs e)
		{
			if (!int.TryParse(e.Value?.ToString(), out var colCount))
				return Task.CompletedTask;
			ColCount = colCount;
			return ColCountChanged.InvokeAsync(colCount);
		}

		private Task ToggleProfile()
		{
			ShowProfile = !ShowProfile;
			return ShowProfileChanged.InvokeAsync(ShowProfile);
		}

		private Task OnTopCellsPercentSelected(ChangeEventArgs e)
		{
			if (!int.TryParse(e.Value?.ToString(), out var topCellsPercent))
				return Task.CompletedTask;
			TopCellsPercent = topCellsPercent;
			return TopCellsPercentChanged.InvokeAsync(topCellsPercent);
		}

		private Task OnLowCellsPercentSelected(ChangeEventArgs e)
		{
			if (!int.TryParse(e.Value?.ToString(), out var lowCellsPercent))
				return Task.CompletedTask;
			LowCellsPercent = lowCellsPercent;
			return LowCellsPercentChanged.InvokeAsync(lowCellsPercent);
		}

		private static string FormatRowPercent(double rowPercent) => rowPercent.ToString("0.##", CultureInfo.InvariantCulture);

		public async ValueTask DisposeAsync()
		{
			_disposed = true;

			if (_module is not null)
			{
				try
				{
					await _module.InvokeVoidAsync("dispose", _canvasRef);
					await _module.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
				catch (ObjectDisposedException) { }
			}

			GC.SuppressFinalize(this);
		}
	}
}
