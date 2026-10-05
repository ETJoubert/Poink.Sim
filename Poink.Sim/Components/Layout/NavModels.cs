namespace Poink.Sim.Components.Layout;

/// <summary>
/// Common shape for a top-level entry in the main nav: a direct link, a dropdown group, or an always-visible cluster of links.
/// </summary>
public abstract record NavEntry(string Label, string Icon);

public sealed record NavLinkItem(string Route, string Label, string Icon) : NavEntry(Label, Icon);

public sealed record NavGroupItem(string Label, string Icon, NavLinkItem[] Children) : NavEntry(Label, Icon);

public sealed record NavClusterItem(string Label, string Icon, string Description, NavLinkItem[] Children) : NavEntry(Label, Icon);
