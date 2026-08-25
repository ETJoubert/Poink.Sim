namespace Poink.Sim.Components.Shared.PageNav;

/// <summary>
/// Single source of truth for the page-header nav layout cycle every page (and MainLayout)
/// exposes via its "$ BRAND" click target: horizontal-expanded, horizontal-collapsed,
/// sidebar-expanded, sidebar-collapsed, back to horizontal-expanded.
/// </summary>
public sealed class PageNavState
{
    public int State { get; private set; }
    public bool SidebarMode => State >= 2;
    public bool NavCollapsed => State is 1 or 3;

    public void Cycle() => State = (State + 1) % 4;
}
