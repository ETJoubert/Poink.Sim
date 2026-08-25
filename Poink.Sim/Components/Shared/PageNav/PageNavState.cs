namespace Poink.Sim.Components.Shared.PageNav;

// Single source of truth for the page-header nav layout cycle every page (and MainLayout) exposes
// via its "$ BRAND" click target: 0 horizontal-expanded -> 1 horizontal-collapsed -> 2 sidebar-expanded
// -> 3 sidebar-collapsed -> back to 0. Previously hand-copied as _navState/SidebarMode/NavCollapsed/
// CycleNavState into every page's code-behind; now each page just holds one instance of this.
public sealed class PageNavState
{
    public int State { get; private set; }
    public bool SidebarMode => State >= 2;
    public bool NavCollapsed => State is 1 or 3;

    public void Cycle() => State = (State + 1) % 4;
}
