using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Code.Models;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Simulation;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages;

public partial class CodePage : ComponentBase, IDisposable
{
    [Inject] private ICodeGraphProvider GraphProvider { get; set; } = default!;

    [Inject] private ITrainingService Training { get; set; } = default!;

    [Inject] private ISimulationEngine Simulation { get; set; } = default!;

    [Inject] private ILogger<CodePage> Logger { get; set; } = default!;

    private const double MinZoom = 0.25;
    private const double MaxZoom = 1.3;
    private const double ZoomStep = 0.05;
    private const double DefaultZoom = 0.5;

    private CodeGraph _graph = new([], []);
    private string? _selectedId;
    private HashSet<string> _highlighted = [];
    private HashSet<CodeConcept> _visibleConcepts = [];
    private double _zoom = DefaultZoom;
    private bool _animate;

    private static readonly CodeConcept[] AllConcepts = Enum.GetValues<CodeConcept>();

    protected override void OnInitialized()
    {
        _graph = GraphProvider.Build();
        _visibleConcepts = [.. AllConcepts];

        Training.OnChanged += HandleServiceChanged;
        Simulation.OnChanged += HandleServiceChanged;

        Logger.LogInformation(
            "Code page opened: {NodeCount} objects, {PortCount} ports, {EdgeCount} wires, training={TrainingState}, replay={ReplayState}",
            _graph.Nodes.Count,
            _graph.Nodes.Sum(n => n.Inputs.Count + n.Outputs.Count),
            _graph.Edges.Count,
            Training.IsRunning ? "running" : "idle",
            Simulation.Snapshot is null ? "not loaded" : "loaded");

        foreach (var unresolved in FindUnresolvedWires())
            Logger.LogWarning("Code page wire does not resolve to a declared port: {Wire}", unresolved);
    }

    private IEnumerable<string> FindUnresolvedWires()
    {
        foreach (var edge in _graph.Edges)
        {
            var from = _graph.Nodes.FirstOrDefault(n => n.Id == edge.FromId);
            var to = _graph.Nodes.FirstOrDefault(n => n.Id == edge.ToId);

            if (from is null || !from.Outputs.Any(p => p.Label == edge.FromPort))
                yield return $"{edge.FromId}.{edge.FromPort} -> {edge.ToId}.{edge.ToPort} (output missing)";
            else if (to is null || !to.Inputs.Any(p => p.Label == edge.ToPort))
                yield return $"{edge.FromId}.{edge.FromPort} -> {edge.ToId}.{edge.ToPort} (input missing)";
        }
    }

    private void HandleServiceChanged()
    {
        _graph = GraphProvider.Build();
        _ = InvokeAsync(StateHasChanged);
    }

    private int PortCount => _graph.Nodes.Sum(n => n.Inputs.Count + n.Outputs.Count);

    private CodeNode? SelectedNode =>
        _selectedId is null ? null : _graph.Nodes.FirstOrDefault(n => n.Id == _selectedId);

    private void SelectNode(string nodeId)
    {
        if (_selectedId == nodeId)
        {
            ClearSelection();
            return;
        }

        _selectedId = nodeId;
        _highlighted = BuildFlowClosure(nodeId);

        Logger.LogInformation("Code page traced {NodeId}: {HighlightedCount} objects on its flow path", nodeId, _highlighted.Count);
    }

    private void ClearSelection()
    {
        _selectedId = null;
        _highlighted = [];
    }

    private HashSet<string> BuildFlowClosure(string nodeId)
    {
        var traced = new HashSet<string> { nodeId };
        var traceable = _graph.Edges.Where(e => e.Kind != CodeEdgeKind.Drives).ToList();

        Walk(nodeId, id => traceable.Where(e => e.ToId == id).Select(e => e.FromId));
        Walk(nodeId, id => traceable.Where(e => e.FromId == id).Select(e => e.ToId));

        return traced;

        void Walk(string start, Func<string, IEnumerable<string>> next)
        {
            var pending = new Queue<string>();
            pending.Enqueue(start);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var neighbour in next(current))
                {
                    if (traced.Add(neighbour))
                        pending.Enqueue(neighbour);
                }
            }
        }
    }

    private bool IsConceptVisible(CodeConcept concept) => _visibleConcepts.Contains(concept);

    private void ToggleConcept(CodeConcept concept)
    {
        if (!_visibleConcepts.Remove(concept))
            _visibleConcepts.Add(concept);

        if (_visibleConcepts.Count == 0)
            _visibleConcepts = [.. AllConcepts];
    }

    private static string LegendTooltip(CodeConcept concept) => concept switch
    {
        CodeConcept.Config => "Config: the settings you choose before a run. Dashed cards, wired into whatever they configure. Click to show or hide.",
        CodeConcept.Data => "Data: tick source, episode sampling and bar aggregation. Click to show or hide.",
        CodeConcept.Inputs => "Inputs: the observation, the encoders that flatten it and the per-feature normalizers. Click to show or hide.",
        CodeConcept.Network => "Network: the composed brain and the trainable TorchSharp module. Click to show or hide.",
        CodeConcept.Outputs => "Outputs: decoding logits into a direction and sampling the action head levels. Click to show or hide.",
        CodeConcept.Actions => "Actions: the decision record the brain emits. Click to show or hide.",
        CodeConcept.Trader => "Trader: order execution, position maintenance and the account state. Click to show or hide.",
        CodeConcept.Simulation => "Simulation: the runners that drive the loop, headless for training and interactive for replay. Click to show or hide.",
        CodeConcept.Rewards => "Rewards: scoring each step and turning the episode into a weight update. Click to show or hide.",
        _ => concept.ToString()
    };

    private void ToggleAnimation()
    {
        _animate = !_animate;
        Logger.LogInformation("Code page wire animation {State}", _animate ? "started" : "stopped");
    }

    private string AnimateTooltip => _animate
        ? "Stop the flowing animation on the wires and ports"
        : "Animate the wires and ports so each signal's direction of travel is visible";

    private void ZoomIn() => _zoom = Math.Min(MaxZoom, Math.Round(_zoom + ZoomStep, 2));

    private void ZoomOut() => _zoom = Math.Max(MinZoom, Math.Round(_zoom - ZoomStep, 2));

    private void HandleWheelZoom(double deltaY)
    {
        if (deltaY < 0)
            ZoomIn();
        else if (deltaY > 0)
            ZoomOut();
    }

    private void ResetView()
    {
        _zoom = DefaultZoom;
        _visibleConcepts = [.. AllConcepts];
        ClearSelection();
    }

    private static string ResetTooltip => "Reset zoom, show every concept and clear the traced signal path. Leaves the animation toggle alone.";

    private string ZoomLabel => (_zoom * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private bool HasLiveValues => Training.IsRunning || Simulation.Snapshot is not null;

    private string StateLabel => Training.IsRunning
        ? Training.IsPaused ? "TRAINING PAUSED" : "TRAINING LIVE"
        : Simulation.Snapshot is not null ? "REPLAY LOADED" : "IDLE";

    private string StateClass => HasLiveValues ? "code-header-state-live" : "code-header-state-idle";

    public void Dispose()
    {
        Training.OnChanged -= HandleServiceChanged;
        Simulation.OnChanged -= HandleServiceChanged;
    }
}
