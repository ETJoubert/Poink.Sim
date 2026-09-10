using Poink.Sim.Components.Pages.TradeMap.Models;

namespace Poink.Sim.Components.Pages.TradeMap.Helpers
{
	/// <summary>Shared label/slug mapping for TradeMapPartKind, kept in one place since both the page legend and the node cards need it.</summary>
	public static class TradeMapKindHelper
	{
		public static string Label(TradeMapPartKind kind) =>
			kind switch
			{
				TradeMapPartKind.Page => "PAGE",
				TradeMapPartKind.Component => "COMPONENT",
				TradeMapPartKind.Service => "SERVICE",
				TradeMapPartKind.Helper => "HELPER",
				TradeMapPartKind.ExternalApi => "EXTERNAL API",
				TradeMapPartKind.DataStore => "DATA STORE",
				_ => "",
			};

		public static string Slug(TradeMapPartKind kind) =>
			kind switch
			{
				TradeMapPartKind.Page => "page",
				TradeMapPartKind.Component => "component",
				TradeMapPartKind.Service => "service",
				TradeMapPartKind.Helper => "helper",
				TradeMapPartKind.ExternalApi => "api",
				TradeMapPartKind.DataStore => "data",
				_ => "component",
			};
	}
}
