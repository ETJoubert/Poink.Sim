using System.Collections.Generic;
using Poink.Sim.Components.Pages.Arch.Models;

namespace Poink.Sim.Components.Pages.Arch.Helpers;

public static class ArchNodeLayoutHelper
{
    public const double CardWidth = 210;
    public const double HeaderHeight = 24;
    public const double PortsPad = 8;
    public const double PortRowHeight = 16;
    public const double FooterHeight = 32;
    public const double PortClearance = 7;

    public static double CardHeightFor(ArchComponentNode node)
    {
        var bodyRows = System.Math.Max(node.Inputs.Count, node.Outputs.Count);
        return HeaderHeight + PortsPad * 2 + bodyRows * PortRowHeight + FooterHeight;
    }

    public static double PortCenterY(int index) =>
        HeaderHeight + PortsPad + index * PortRowHeight + PortRowHeight / 2;

    public static int IndexOfPort(IReadOnlyList<ArchPort> ports, string name)
    {
        for (var index = 0; index < ports.Count; index++)
        {
            if (ports[index].Name == name)
                return index;
        }
        return -1;
    }

    public static IReadOnlyList<ArchPortView> BuildPortViews(ArchComponentNode node)
    {
        var views = new List<ArchPortView>(node.Inputs.Count + node.Outputs.Count);

        for (var index = 0; index < node.Inputs.Count; index++)
        {
            var port = node.Inputs[index];
            views.Add(new ArchPortView(port.Name, IsOutput: false, PortCenterY(index), port.WiredFrom is not null, port.Tooltip));
        }

        for (var index = 0; index < node.Outputs.Count; index++)
        {
            var port = node.Outputs[index];
            views.Add(new ArchPortView(port.Name, IsOutput: true, PortCenterY(index), IsConnected: false, port.Tooltip));
        }

        return views;
    }

    // A pulled-back tip keeps an arrowhead from burying itself under a port dot at the line's target.
    public static (double X, double Y) PullBack((double X, double Y) from, (double X, double Y) target)
    {
        var dx = target.X - from.X;
        var dy = target.Y - from.Y;
        var length = System.Math.Sqrt(dx * dx + dy * dy);
        return length < PortClearance
            ? target
            : (target.X - dx / length * PortClearance, target.Y - dy / length * PortClearance);
    }
}
