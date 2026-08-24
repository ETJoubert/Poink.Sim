using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.ConfirmDialog;

public partial class ConfirmDialog : ComponentBase
{
	[Parameter, EditorRequired]
	public string Title { get; set; } = "";

	[Parameter, EditorRequired]
	public string Message { get; set; } = "";

	[Parameter]
	public string ConfirmLabel { get; set; } = "CONFIRM";

	[Parameter]
	public string CancelLabel { get; set; } = "CANCEL";

	[Parameter]
	public EventCallback OnConfirm { get; set; }

	[Parameter]
	public EventCallback OnCancel { get; set; }
}
