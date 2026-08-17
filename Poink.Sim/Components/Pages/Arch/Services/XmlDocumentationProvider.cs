using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Poink.Sim.Components.Pages.Arch.Services;

internal sealed partial class XmlDocumentationProvider : IXmlDocumentationProvider
{
    private readonly ILogger<XmlDocumentationProvider> _logger;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _index;

    public XmlDocumentationProvider(ILogger<XmlDocumentationProvider> logger)
    {
        _logger = logger;
        _index = new Lazy<IReadOnlyDictionary<string, string>>(BuildIndex);
    }

    public string? SummaryFor(Type type) =>
        _index.Value.TryGetValue($"T:{type.FullName}", out var summary) ? summary : null;

    private IReadOnlyDictionary<string, string> BuildIndex()
    {
        var path = Path.ChangeExtension(typeof(XmlDocumentationProvider).Assembly.Location, ".xml");
        if (!File.Exists(path))
        {
            _logger.LogWarning("XML documentation file not found at {Path}; chip summaries fall back to a generic description", path);
            return new Dictionary<string, string>();
        }

        var map = new Dictionary<string, string>();
        var document = XDocument.Load(path);
        foreach (var member in document.Descendants("member"))
        {
            var name = (string?)member.Attribute("name");
            var summary = member.Element("summary");
            if (name is null || summary is null)
                continue;

            map[name] = CleanSummary(summary.Value);
        }

        _logger.LogInformation("XML documentation index built: {MemberCount} documented members read from {Path}", map.Count, path);
        return map;
    }

    private static string CleanSummary(string raw)
    {
        var joined = string.Join(" ", raw.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
        var withoutCrefs = CrefExpression().Replace(joined, match => SimpleName(match.Groups[1].Value));
        return TagExpression().Replace(withoutCrefs, "").Trim();
    }

    // "T:Poink.Sim.Trader.Trader" -> "Trader"; leaves member crefs ("M:", "P:") as their
    // simple member name rather than trying to also show the declaring type.
    private static string SimpleName(string cref)
    {
        var colon = cref.IndexOf(':');
        var path = colon >= 0 ? cref[(colon + 1)..] : cref;
        var lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path[(lastDot + 1)..] : path;
    }

    [GeneratedRegex("""<see\s+cref="([^"]+)"\s*/>""")]
    private static partial Regex CrefExpression();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagExpression();
}
