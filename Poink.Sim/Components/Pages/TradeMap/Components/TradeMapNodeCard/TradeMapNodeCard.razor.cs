using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Components.Pages.TradeMap.Helpers;
using Poink.Sim.Components.Pages.TradeMap.Models;

namespace Poink.Sim.Components.TradeMap
{
	public partial class TradeMapNodeCard : ComponentBase
	{
		[Parameter, EditorRequired]
		public TradeMapNode Node { get; set; } = default!;

		[Parameter]
		public IReadOnlyList<TradeMapPortView> Ports { get; set; } = [];

		[Parameter]
		public bool IsSelected { get; set; }

		[Parameter]
		public bool IsDimmed { get; set; }

		[Parameter]
		public EventCallback<string> OnSelected { get; set; }

		private string CardClass
		{
			get
			{
				var selected = IsSelected ? " tmnc-selected" : string.Empty;
				var dimmed = IsDimmed ? " tmnc-dimmed" : string.Empty;
				return $"tmnc-card tmnc-{TradeMapKindHelper.Slug(Node.Kind)}{selected}{dimmed}";
			}
		}

		private string KindLabel => TradeMapKindHelper.Label(Node.Kind);

		private string Tooltip => $"{KindLabel} -- {Node.Title}. Click to trace its connectors and open its detail panel.";

		private static string PortClass(TradeMapPortView port)
		{
			var side = port.IsOutput ? "tmnc-port-out" : "tmnc-port-in";
			var open = port.IsConnected ? string.Empty : " tmnc-port-open";
			return $"tmnc-port {side}{open}";
		}

		private static string PortStyle(TradeMapPortView port) =>
			string.Create(CultureInfo.InvariantCulture, $"top:{port.CenterY:0.##}px;");

		private Task RaiseSelected() => OnSelected.InvokeAsync(Node.Id);

		private Task HandleKeyDown(KeyboardEventArgs e) => e.Key is "Enter" or " " ? RaiseSelected() : Task.CompletedTask;
	}
}
