using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.ViewDatatable;

public partial class ViewDatatable : ComponentBase
{
    [Parameter, EditorRequired]
    public List<object> Items { get; set; } = new();

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public bool ShowRowNumbers { get; set; } = false;

    private string _displayTitle = string.Empty;

    [Parameter]
    public List<string>? ExcludeProperties { get; set; }

    [Parameter]
    public string EmptyMessage { get; set; } = "No data to display.";

    private record ColumnDef(string Header, Func<object, string> GetValue);

    private List<ColumnDef> _columns = new();

    protected override void OnParametersSet()
    {
        BuildColumns();
        _displayTitle = !string.IsNullOrEmpty(Title)
            ? Title
            : Items.Count > 0 ? Items.First().GetType().Name.ToUpperInvariant() : "DATA";
    }

    private void BuildColumns()
    {
        _columns.Clear();

        if (Items.Count == 0)
            return;

        var firstItem = Items.First();

        // Primitive / string items — single column showing the raw value
        if (firstItem is string || firstItem.GetType().IsPrimitive)
        {
            _columns.Add(new ColumnDef("RAW LINE", item => item?.ToString() ?? "-"));
            return;
        }

        var properties = firstItem.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken)
            .ToArray();

        if (ExcludeProperties?.Count > 0)
            properties = properties.Where(p => !ExcludeProperties.Contains(p.Name)).ToArray();

        foreach (var prop in properties)
        {
            var captured = prop;
            _columns.Add(new ColumnDef(
                FormatHeader(captured.Name),
                item => FormatValue(captured.GetValue(item))
            ));
        }
    }

    private static string FormatHeader(string name)
    {
        var spaced = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
        return spaced.ToUpperInvariant();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "-",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        TimeOnly t => t.ToString("HH:mm"),
        decimal dec => dec.ToString("N2"),
        double dbl => dbl.ToString("N2"),
        float flt => flt.ToString("N2"),
        bool b => b ? "YES" : "NO",
        _ => value.ToString() ?? "-"
    };
}
