using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Shared.LookbackWindowBar;

/// <summary>Bar/tick lookback window control -- a rail whose highlighted segment can be dragged by
/// its right edge (resizes, anchoring the far edge) or by its body (pans, width unchanged), plus
/// plain number inputs for both values. Shared by SimulationPage and TradeChartPanel.</summary>
public partial class LookbackWindowBar : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public int MaxWindow { get; set; }

    [Parameter]
    public int MinWidth { get; set; } = 1;

    [Parameter]
    public int Width { get; set; }

    [Parameter]
    public EventCallback<int> WidthChanged { get; set; }

    [Parameter]
    public int Offset { get; set; }

    [Parameter]
    public EventCallback<int> OffsetChanged { get; set; }

    [Parameter]
    public string Label { get; set; } = "LOOKBACK WINDOW";

    [Parameter]
    public string RailTitle { get; set; } =
        "The highlighted segment is the window itself -- drag either edge to resize it, or drag the bar to move the whole window";

    [Parameter]
    public string SizeInputTitle { get; set; } = "Number of steps the window spans";

    [Parameter]
    public string OffsetLabelTitle { get; set; } =
        "Steps back from the most recent step to the end of the window -- 0 keeps the window anchored to now";

    [Parameter]
    public string OffsetInputTitle { get; set; } = "Steps back from the most recent step to the end of the window";

    private ElementReference _railRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<LookbackWindowBar>? _dotNetRef;

    private double FillPercent => MaxWindow <= 0 ? 0 : Math.Clamp(Width, 0, MaxWindow) * 100.0 / MaxWindow;
    private double OffsetFillPercent => MaxWindow <= 0 ? 0 : Math.Clamp(Offset, 0, MaxWindow) * 100.0 / MaxWindow;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/lookbackWindowBar.js");
        _dotNetRef = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("create", _railRef, _dotNetRef);
    }

    private Task OnWidthInput(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), out var value) ? WidthChanged.InvokeAsync(value) : Task.CompletedTask;

    private Task OnOffsetInput(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), out var value) ? OffsetChanged.InvokeAsync(value) : Task.CompletedTask;

    [JSInvokable]
    public async Task OnWindowChangedAsync(double width, double offset)
    {
        var newWidth = Math.Clamp((int)Math.Round(width), MinWidth, Math.Max(MinWidth, MaxWindow));
        var newOffset = Math.Clamp((int)Math.Round(offset), 0, Math.Max(0, MaxWindow - 1));

        if (newWidth != Width)
            await WidthChanged.InvokeAsync(newWidth);
        if (newOffset != Offset)
            await OffsetChanged.InvokeAsync(newOffset);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _railRef);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        _dotNetRef?.Dispose();
    }
}
