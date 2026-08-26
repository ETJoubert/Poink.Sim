using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Shared.SimTimeline;

public partial class SimTimeline : ComponentBase, IDisposable, IAsyncDisposable
{
	[Inject]
	private ISimulationEngine Service { get; set; } = default!;

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	private ElementReference _railRef;
	private IJSObjectReference? _dragScript;
	private DotNetObjectReference<SimTimeline>? _dotNetRef;

	// Move updates the rail purely in JS (no round trip); pointerup is the only moment this
	// commits, since Seek replays every tick between the old and new cursor and isn't cheap.
	private const string SeekDragScript = """
		(function() {
		    function bindSeek(rail, dotNetRef) {
		        if (!rail || rail.dataset.bound) return;
		        rail.dataset.bound = '1';
		        var fill = rail.querySelector('.stl-fill');
		        var marker = rail.querySelector('.stl-marker');

		        function fractionFor(ev) {
		            var rect = rail.getBoundingClientRect();
		            return Math.min(1, Math.max(0, (ev.clientX - rect.left) / rect.width));
		        }
		        function paint(fraction) {
		            var pct = (fraction * 100) + '%';
		            if (fill) fill.style.width = pct;
		            if (marker) marker.style.left = pct;
		        }

		        rail.addEventListener('pointerdown', function(e) {
		            e.preventDefault();
		            rail.setPointerCapture(e.pointerId);
		            rail.classList.add('stl-dragging');
		            paint(fractionFor(e));

		            function onMove(ev) {
		                paint(fractionFor(ev));
		            }
		            function onUp(ev) {
		                rail.releasePointerCapture(e.pointerId);
		                rail.removeEventListener('pointermove', onMove);
		                rail.removeEventListener('pointerup', onUp);
		                rail.classList.remove('stl-dragging');
		                dotNetRef.invokeMethodAsync('OnSeekCommitted', fractionFor(ev));
		            }
		            rail.addEventListener('pointermove', onMove);
		            rail.addEventListener('pointerup', onUp);
		        });
		    }
		    return { bindSeek: bindSeek };
		})()
		""";

	protected override void OnInitialized() => Service.OnProgress += HandleProgress;

	private void HandleProgress() => _ = InvokeAsync(StateHasChanged);

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (Service.Progress is null)
			return;

		if (firstRender)
			_dragScript = await JS.InvokeAsync<IJSObjectReference>("eval", SeekDragScript);

		if (_dragScript is null)
			return;
		_dotNetRef ??= DotNetObjectReference.Create(this);
		await _dragScript.InvokeVoidAsync("bindSeek", _railRef, _dotNetRef);
	}

	[JSInvokable]
	public void OnSeekCommitted(double fraction)
	{
		if (Service.Progress is not { } p)
			return;
		int targetIndex = (int)Math.Round(Math.Clamp(fraction, 0, 1) * p.TotalCount);
		Service.Seek(targetIndex);
	}

	public void Dispose() => Service.OnProgress -= HandleProgress;

	public async ValueTask DisposeAsync()
	{
		_dotNetRef?.Dispose();
		if (_dragScript is not null)
		{
			try
			{
				await _dragScript.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}
	}

	private static double Percent(SimulationProgress p)
	{
		if (p.CurrentTimestampUtc is not { } cur)
			return 0;
		var totalTicks = (p.RangeToUtc - p.RangeFromUtc).Ticks;
		if (totalTicks <= 0)
			return 0;
		return Math.Clamp((cur - p.RangeFromUtc).Ticks * 100.0 / totalTicks, 0, 100);
	}

	private static string Pct(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
