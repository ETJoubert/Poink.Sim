using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Calls.Models;
using Poink.Sim.Components.Pages.Calls.Services;

namespace Poink.Sim.Components.Pages;

public partial class CallsPage : ComponentBase
{
	[Inject]
	private ICallGraphIndex Index { get; set; } = default!;

	[Inject]
	private ILogger<CallsPage> Logger { get; set; } = default!;

	private CallGraphIndexResult? _result;
	private string _search = "";
	private string? _selectedTypeId;
	private string? _selectedMethodId;
	private HashSet<string> _visibleKinds = [];

	protected override async Task OnInitializedAsync()
	{
		_result = await Index.GetAsync();
		_visibleKinds = [.. AllKinds];

		Logger.LogInformation(
			"Calls page opened: {TypeCount} types, {MethodCount} methods, {EdgeCount} call edges indexed by Roslyn in {ElapsedMs}ms",
			_result.Stats.TypeCount,
			_result.Stats.MethodCount,
			_result.Stats.EdgeCount,
			_result.Stats.BuildDuration.TotalMilliseconds
		);
	}

	private IReadOnlyList<string> AllKinds =>
		_result is null
			? []
			: [.. _result.Types.Select(t => t.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal)];

	private IReadOnlyList<CallGraphType> FilteredTypes =>
		_result is null ? [] : [.. _result.Types.Where(t => _visibleKinds.Contains(t.Kind) && MatchesSearch(t))];

	private bool MatchesSearch(CallGraphType type) =>
		_search.Length == 0
		|| type.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
		|| type.Namespace.Contains(_search, StringComparison.OrdinalIgnoreCase)
		|| type.Methods.Any(m => m.Signature.Contains(_search, StringComparison.OrdinalIgnoreCase));

	private bool IsKindVisible(string kind) => _visibleKinds.Contains(kind);

	private void ToggleKind(string kind)
	{
		if (!_visibleKinds.Remove(kind))
			_visibleKinds.Add(kind);

		if (_visibleKinds.Count == 0)
			_visibleKinds = [.. AllKinds];
	}

	private string KindTooltip(string kind)
	{
		var count = _result?.Types.Count(t => t.Kind == kind) ?? 0;
		return $"{kind}: {count} type{(count == 1 ? "" : "s")}. Click to show or hide.";
	}

	private CallGraphType? SelectedType =>
		_selectedTypeId is null ? null : _result?.Types.FirstOrDefault(t => t.Id == _selectedTypeId);

	private CallGraphMethod? SelectedMethod =>
		SelectedType is null || _selectedMethodId is null
			? null
			: SelectedType.Methods.FirstOrDefault(m => m.Id == _selectedMethodId);

	private void OnSearchChanged(ChangeEventArgs args) => _search = args.Value?.ToString() ?? "";

	private void SelectType(string typeId)
	{
		_selectedTypeId = typeId;
		_selectedMethodId = null;
	}

	private void SelectMethod(string methodId) => _selectedMethodId = methodId;

	private void SelectEdge(CallGraphEdge edge)
	{
		var owningType = _result?.Types.FirstOrDefault(t => t.Methods.Any(m => m.Id == edge.MethodId));
		if (owningType is null)
			return;

		_selectedTypeId = owningType.Id;
		_selectedMethodId = edge.MethodId;
	}

	private void ClearSelection()
	{
		_selectedTypeId = null;
		_selectedMethodId = null;
	}
}
