using System;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Pages.Analize
{
	public partial class AnalizePage : ComponentBase
	{
		private bool _leftPanelCollapsed;
		private bool _rightPanelCollapsed;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;
	}
}
