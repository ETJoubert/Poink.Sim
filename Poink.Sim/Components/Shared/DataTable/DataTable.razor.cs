using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Core;

namespace Poink.Sim.Components.Shared.DataTable;

public partial class DataTable<TItem> : IAsyncDisposable
{
	/// <summary>Used when binding a plain list (non-paged). Ignored when <see cref="PagedData"/> is set.</summary>
	[Parameter]
	public List<TItem> Items { get; set; } = new();

	/// <summary>When set, drives the table rows and shows the pagination bar.</summary>
	[Parameter]
	public PagedResponse<TItem>? PagedData { get; set; }

	/// <summary>Raised when the user clicks a page button. Argument is the requested 1-based page number.</summary>
	[Parameter]
	public EventCallback<int> OnPageChange { get; set; }

	/// <summary>Raised when the user selects a new page size. Argument is the new page size.</summary>
	[Parameter]
	public EventCallback<int> OnPageSizeChange { get; set; }

	/// <summary>
	/// When set, a search input is rendered and this callback is fired (debounced 350 ms) with the current
	/// search term whenever the user pauses typing. The parent is responsible for calling its SearchAsync
	/// and updating <see cref="PagedData"/>. Searching automatically resets the page to 1.
	/// </summary>
	[Parameter]
	public EventCallback<string> OnSearch { get; set; }

	/// <summary>Placeholder text shown inside the search input.</summary>
	[Parameter]
	public string SearchPlaceholder { get; set; } = "Search...";

	/// <summary>Options shown in the row-count selector. Defaults to 10, 25, 50, 100.</summary>
	[Parameter]
	public List<int> PageSizeOptions { get; set; } = [10, 25, 50, 100];

	private List<TItem> EffectiveItems => PagedData?.Items ?? Items;

	private int RowOffset => PagedData is null ? 0 : (PagedData.Page - 1) * PagedData.PageSize;

	[Parameter]
	public RenderFragment? TableFooter { get; set; }

	[Parameter]
	public RenderFragment<TItem>? CustomActions { get; set; }

	/// <summary>Rendered inside each card (card view), between its header and its reflected
	/// fields -- e.g. a chart preview alongside a row's plain metadata.</summary>
	[Parameter]
	public RenderFragment<TItem>? CardContent { get; set; }

	/// <summary>Rendered inside the detail overlay (opened via the View action/eye icon),
	/// above its reflected field list -- same use case as <see cref="CardContent"/>.</summary>
	[Parameter]
	public RenderFragment<TItem>? DetailContent { get; set; }

	[Parameter]
	public bool ShowActions { get; set; } = true;

	[Parameter]
	public bool ShowRowNumbers { get; set; } = true;

	[Parameter]
	public string ActionsHeader { get; set; } = "ACTIONS";

	[Parameter]
	public EventCallback<TItem> OnEdit { get; set; }

	[Parameter]
	public EventCallback<TItem> OnDelete { get; set; }

	[Parameter]
	public EventCallback<TItem> OnView { get; set; }

	[Parameter]
	public List<string>? ExcludeProperties { get; set; }

	/// <summary>Shown in place of the table/cards when there are no items to display.</summary>
	[Parameter]
	public string EmptyMessage { get; set; } = "No data to display.";

	[Parameter]
	public Func<object?, string>? ValueFormatter { get; set; }

	/// <summary>Optional per-cell CSS class, e.g. to color a PnL value green/red by sign. Applied in
	/// both table cells and card/detail field values.</summary>
	[Parameter]
	public Func<TItem, string, string>? CellClass { get; set; }

	/// <summary>Always show cards: hides the table/card toggle, so the table view is never offered.</summary>
	[Parameter]
	public bool CardViewOnly { get; set; }

	/// <summary>Start with every field ticked in the card view's FIELDS picker, instead of none.</summary>
	[Parameter]
	public bool ShowAllCardFields { get; set; }

	/// <summary>Name of the property whose value heads each card in place of the record's id. Read even when listed in <see cref="ExcludeProperties"/>.</summary>
	[Parameter]
	public string? CardTitleProperty { get; set; }

	/// <summary>Raised when a card is clicked, making cards selectable.</summary>
	[Parameter]
	public EventCallback<TItem> OnCardClick { get; set; }

	/// <summary>Marks cards as selected, e.g. the one the parent is currently filtering by.</summary>
	[Parameter]
	public Func<TItem, bool>? IsCardSelected { get; set; }

	[Parameter]
	public bool EnableMultiSelect { get; set; } = false;

	[Parameter]
	public EventCallback<List<TItem>> OnSelectionChanged { get; set; }

	/// <summary>Rendered inside the selection bar when items are selected. Receives the current selection list.</summary>
	[Parameter]
	public RenderFragment<List<TItem>>? BulkActions { get; set; }

	private bool _viewDefaultsApplied;

	protected override void OnParametersSet()
	{
		if (_viewDefaultsApplied)
			return;

		_viewDefaultsApplied = true;
		_isCardView = CardViewOnly;
		if (ShowAllCardFields)
			_cardFieldSelection = GetProperties().Select(p => p.Name).ToHashSet();
	}

	private string? CardTitle(TItem item) =>
		CardTitleProperty is null ? null : FormatValue(typeof(TItem).GetProperty(CardTitleProperty)?.GetValue(item));

	private bool IsCardHighlighted(TItem item) => IsSelected(item) || (IsCardSelected?.Invoke(item) ?? false);

	private async Task HandleCardClick(TItem item)
	{
		if (EnableMultiSelect)
			ToggleSelectItem(item);
		if (OnCardClick.HasDelegate)
			await OnCardClick.InvokeAsync(item);
	}

	private HashSet<TItem> _selectedItems = new();

	public List<TItem> SelectedItems => _selectedItems.ToList();

	private bool IsSelected(TItem item) => _selectedItems.Contains(item);

	private bool AllCurrentPageSelected =>
		EffectiveItems.Count > 0 && EffectiveItems.All(i => _selectedItems.Contains(i));

	private void ToggleSelectItem(TItem item)
	{
		if (!_selectedItems.Remove(item))
			_selectedItems.Add(item);
		if (OnSelectionChanged.HasDelegate)
			_ = OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
	}

	private void ToggleSelectAll()
	{
		if (AllCurrentPageSelected)
		{
			foreach (var item in EffectiveItems)
				_selectedItems.Remove(item);
		}
		else
		{
			foreach (var item in EffectiveItems)
				_selectedItems.Add(item);
		}
		if (OnSelectionChanged.HasDelegate)
			_ = OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
	}

	private void ClearSelection()
	{
		_selectedItems.Clear();
		if (OnSelectionChanged.HasDelegate)
			_ = OnSelectionChanged.InvokeAsync(_selectedItems.ToList());
	}

	// ── View mode / detail panel state ───────────────────────────────────────

	private bool _isCardView = false;
	private bool _showDetail = false;
	private TItem _detailItem = default!;

	private void OpenDetail(TItem item)
	{
		_detailItem = item;
		_showDetail = true;
		if (OnView.HasDelegate)
			_ = OnView.InvokeAsync(item);
	}

	private void CloseDetail()
	{
		_showDetail = false;
		_detailItem = default!;
	}

	private async Task EditFromDetail()
	{
		if (OnEdit.HasDelegate)
			await OnEdit.InvokeAsync(_detailItem);
		CloseDetail();
	}

	// ── Delete confirmation ───────────────────────────────────────────────────

	private bool _showDeleteConfirm = false;
	private TItem _pendingDeleteItem = default!;

	private void RequestDelete(TItem item)
	{
		_pendingDeleteItem = item;
		_showDeleteConfirm = true;
	}

	private void CancelDelete()
	{
		_showDeleteConfirm = false;
		_pendingDeleteItem = default!;
	}

	private async Task ConfirmDelete()
	{
		if (OnDelete.HasDelegate)
			await OnDelete.InvokeAsync(_pendingDeleteItem);
		_showDeleteConfirm = false;
		_pendingDeleteItem = default!;
		if (_showDetail)
			CloseDetail();
	}

	private PropertyInfo? _cachedIdProperty;

	private PropertyInfo? GetIdProperty()
	{
		if (_cachedIdProperty != null)
			return _cachedIdProperty;
		var props = typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance);
		_cachedIdProperty =
			props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
			?? props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
			?? props.FirstOrDefault();
		return _cachedIdProperty;
	}

	// ── Column / field selection ──────────────────────────────────────────────

	private bool _showGearPanel = false;
	private bool _showTableColumnPicker = false;
	private bool _showCardColumnPicker = false;

	// null = all visible (lazy init so no OnParametersSet wiring needed)
	private HashSet<string>? _tableColumnSelection;
	private HashSet<string> _cardFieldSelection = new();

	// 0 = auto.
	private int _cardsPerRow = 0;

	private string CardsGridStyle =>
		_cardsPerRow == 0 ? "repeat(auto-fill, minmax(220px, 1fr))" : $"repeat({_cardsPerRow}, 1fr)";

	// ── Column resizing ─────────────────────────────────────────────────────────

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	private ElementReference _tableRef;
	private IJSObjectReference? _resizeInstance;
	private DotNetObjectReference<DataTable<TItem>>? _dotNetRef;

	private const string ActionsColumnKey = "__actions";
	private const string RowNumberColumnKey = "__rownum";
	private const int ActionsColumnMinWidth = 120;

	private Dictionary<string, int> _columnWidths = new();

	private int? GetColumnWidth(string key) => _columnWidths.TryGetValue(key, out var w) ? w : null;

	[JSInvokable]
	public void SetColumnWidth(string key, double width)
	{
		_columnWidths[key] = (int)Math.Round(width);
		StateHasChanged();
	}

	private const string ResizeScript = """
		(function() {
		    function bind(table, dotNetRef) {
		        if (!table) return;
		        var handles = table.querySelectorAll('.data-table-col-resizer');
		        handles.forEach(function(handle) {
		            if (handle.dataset.bound) return;
		            handle.dataset.bound = '1';
		            handle.addEventListener('pointerdown', function(e) {
		                e.preventDefault();
		                var th = handle.parentElement;
		                var key = th.dataset.colKey;
		                var minWidth = parseInt(handle.dataset.minWidth, 10) || 60;
		                var startX = e.clientX;
		                var startWidth = th.getBoundingClientRect().width;
		                handle.setPointerCapture(e.pointerId);

		                function clamp(ev) {
		                    return Math.max(minWidth, startWidth + (ev.clientX - startX));
		                }
		                function onMove(ev) {
		                    th.style.width = clamp(ev) + 'px';
		                }
		                function onUp(ev) {
		                    handle.releasePointerCapture(e.pointerId);
		                    handle.removeEventListener('pointermove', onMove);
		                    handle.removeEventListener('pointerup', onUp);
		                    var finalWidth = clamp(ev);
		                    th.style.width = finalWidth + 'px';
		                    dotNetRef.invokeMethodAsync('SetColumnWidth', key, finalWidth);
		                }
		                handle.addEventListener('pointermove', onMove);
		                handle.addEventListener('pointerup', onUp);
		            });
		        });
		    }
		    return { bind: bind };
		})()
		""";

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			_dotNetRef = DotNetObjectReference.Create(this);
			_resizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ResizeScript);
		}

		if (!_isCardView && EffectiveItems.Count > 0 && _resizeInstance != null)
		{
			await _resizeInstance.InvokeVoidAsync("bind", _tableRef, _dotNetRef);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_resizeInstance is not null)
		{
			try
			{
				await _resizeInstance.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}
		_dotNetRef?.Dispose();
	}

	private IEnumerable<PropertyInfo> GetVisibleProperties()
	{
		var props = GetProperties();
		return _tableColumnSelection == null ? props : props.Where(p => _tableColumnSelection.Contains(p.Name));
	}

	private IEnumerable<PropertyInfo> GetCardProperties() =>
		GetProperties().Where(p => _cardFieldSelection.Contains(p.Name));

	private void ToggleTableColumnPicker()
	{
		_showTableColumnPicker = !_showTableColumnPicker;
		_showCardColumnPicker = false;
	}

	private void ToggleCardColumnPicker()
	{
		_showCardColumnPicker = !_showCardColumnPicker;
		_showTableColumnPicker = false;
	}

	private void CloseAllPickers()
	{
		_showTableColumnPicker = false;
		_showCardColumnPicker = false;
	}

	private void CloseAllPanels()
	{
		_showTableColumnPicker = false;
		_showCardColumnPicker = false;
		_showGearPanel = false;
	}

	private void ToggleTableColumn(string name)
	{
		_tableColumnSelection ??= GetProperties().Select(p => p.Name).ToHashSet();
		if (!_tableColumnSelection.Remove(name))
			_tableColumnSelection.Add(name);
	}

	private void SelectAllTableColumns() => _tableColumnSelection = GetProperties().Select(p => p.Name).ToHashSet();

	private void ClearAllTableColumns() => _tableColumnSelection = new HashSet<string>();

	private void ToggleCardField(string name)
	{
		if (!_cardFieldSelection.Remove(name))
			_cardFieldSelection.Add(name);
	}

	private void SelectAllCardFields() => _cardFieldSelection = GetProperties().Select(p => p.Name).ToHashSet();

	private void ClearAllCardFields() => _cardFieldSelection = new HashSet<string>();

	// ── Search / debounce state ────────────────────────────────────────────────

	private string _searchTerm = string.Empty;
	private CancellationTokenSource? _searchCts;

	private async Task HandleSearchInput(ChangeEventArgs e)
	{
		_searchTerm = e.Value?.ToString() ?? string.Empty;

		_searchCts?.Cancel();
		_searchCts?.Dispose();
		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			await Task.Delay(350, token);
			await OnSearch.InvokeAsync(_searchTerm);
		}
		catch (TaskCanceledException) { }
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private PropertyInfo[] GetProperties()
	{
		var properties = typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance);

		if (ExcludeProperties != null && ExcludeProperties.Count > 0)
		{
			properties = properties.Where(p => !ExcludeProperties.Contains(p.Name)).ToArray();
		}

		return properties;
	}

	// Always includes the first 2, last 2, and a window of 3 around the current page; a gap
	// between kept pages yields a null, the caller's ellipsis marker.
	private IEnumerable<int?> GetPageNumbers(int totalPages, int currentPage)
	{
		var pages = new HashSet<int>();

		for (int i = 1; i <= Math.Min(2, totalPages); i++)
			pages.Add(i);
		for (int i = Math.Max(1, totalPages - 1); i <= totalPages; i++)
			pages.Add(i);

		for (int i = Math.Max(1, currentPage - 1); i <= Math.Min(totalPages, currentPage + 1); i++)
			pages.Add(i);

		int? prev = null;
		foreach (var p in pages.OrderBy(x => x))
		{
			if (prev.HasValue && p - prev.Value > 1)
				yield return null;
			yield return p;
			prev = p;
		}
	}

	private static bool IsImageProperty(PropertyInfo prop)
	{
		if (prop.PropertyType != typeof(string))
			return false;
		var name = prop.Name.ToLowerInvariant();
		return name.Contains("image")
			|| name.Contains("img")
			|| name.Contains("photo")
			|| name.Contains("avatar")
			|| name.Contains("thumbnail")
			|| name.Contains("picture");
	}

	private string FormatHeader(string propertyName)
	{
		// Convert PascalCase to UPPER CASE with spaces
		var result = System.Text.RegularExpressions.Regex.Replace(propertyName, "([A-Z])", " $1").Trim();
		return result.ToUpper();
	}

	private string FormatValue(object? value)
	{
		if (ValueFormatter != null)
		{
			return ValueFormatter(value);
		}

		if (value == null)
		{
			return "-";
		}

		// Special formatting for common types
		return value switch
		{
			DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
			DateOnly d => d.ToString("yyyy-MM-dd"),
			TimeOnly t => t.ToString("HH:mm"),
			decimal dec => dec.ToString("N2"),
			double dbl => dbl.ToString("N2"),
			float flt => flt.ToString("N2"),
			bool b => b ? "✓" : "✗",
			_ => value.ToString() ?? "-",
		};
	}

	private string GetCellClass(TItem item, string propName) => CellClass?.Invoke(item, propName) ?? "";
}
