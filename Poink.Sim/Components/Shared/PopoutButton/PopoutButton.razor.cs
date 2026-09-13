using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Shared.PopoutButton;

/// <summary>Header button that opens a panel's pop-out route in its own OS window, reusable by any
/// panel that hosts one (see TradeChartPanel). The click itself is bound in JS, not @onclick, so
/// window.open runs inside the browser's synchronous user-gesture stack instead of after a Blazor
/// Server round-trip -- otherwise popup blockers treat it as script-initiated and block it.</summary>
public partial class PopoutButton : ComponentBase, IAsyncDisposable
{
	[Inject]
	private IJSRuntime JS { get; set; } = null!;

	[Parameter, EditorRequired]
	public string Url { get; set; } = "";

	[Parameter, EditorRequired]
	public string WindowName { get; set; } = "";

	[Parameter]
	public int Width { get; set; } = 900;

	[Parameter]
	public int Height { get; set; } = 640;

	[Parameter]
	public string Title { get; set; } = "Open this panel in its own window";

	private ElementReference _buttonRef;
	private IJSObjectReference? _module;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
			return;

		_module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/popoutWindow.js");
		await _module.InvokeVoidAsync("bind", _buttonRef);
	}

	public async ValueTask DisposeAsync()
	{
		if (_module is not null)
		{
			try
			{
				await _module.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}
	}
}
