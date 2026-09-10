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
				TradeMapPartKind.Panel => "PANEL",
				TradeMapPartKind.Driver => "DRIVER",
				TradeMapPartKind.Helper => "HELPER",
				TradeMapPartKind.ExternalApi => "EXTERNAL API",
				TradeMapPartKind.DataStore => "DATA STORE",
				TradeMapPartKind.UserSpace => "USER SPACE",
				TradeMapPartKind.Coordinator => "COORDINATOR",
				_ => "",
			};

		public static string Slug(TradeMapPartKind kind) =>
			kind switch
			{
				TradeMapPartKind.Page => "page",
				TradeMapPartKind.Panel => "panel",
				TradeMapPartKind.Driver => "driver",
				TradeMapPartKind.Helper => "helper",
				TradeMapPartKind.ExternalApi => "api",
				TradeMapPartKind.DataStore => "data",
				TradeMapPartKind.UserSpace => "userspace",
				TradeMapPartKind.Coordinator => "coordinator",
				_ => "panel",
			};
	}
}
