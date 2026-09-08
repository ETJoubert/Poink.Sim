namespace Poink.Sim.Components.Layout;

/// <summary>
/// Common shape for a top-level entry in the main nav: either a direct link or a dropdown group.
/// </summary>
public abstract record NavEntry(string Label, string Icon);

public sealed record NavLinkItem(string Route, string Label, string Icon) : NavEntry(Label, Icon);

public sealed record NavGroupItem(string Label, string Icon, NavLinkItem[] Children) : NavEntry(Label, Icon);
