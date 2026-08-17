using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Poink.Sim.Components.Pages.Code.Services;

internal sealed partial class CodeSourceIndex : ICodeSourceIndex
{
    private readonly ILogger<CodeSourceIndex> _logger;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _index;

    public CodeSourceIndex(ILogger<CodeSourceIndex> logger)
    {
        _logger = logger;
        _index = new Lazy<IReadOnlyDictionary<string, string>>(BuildIndex);
    }

    public string? PathFor(Type type) =>
        _index.Value.TryGetValue($"{type.Namespace}.{type.Name}", out var path) ? path : null;

    private IReadOnlyDictionary<string, string> BuildIndex()
    {
        var root = ProjectRoot();
        var map = new Dictionary<string, string>();
        var fileCount = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
                continue;

            fileCount++;
            IndexFile(file, root, map);
        }

        _logger.LogInformation(
            "Code source index built: {TypeCount} types resolved across {FileCount} source files under {Root}",
            map.Count, fileCount, root);

        return map;
    }

    private static void IndexFile(string file, string root, Dictionary<string, string> map)
    {
        var text = File.ReadAllText(file);
        var namespaceMatch = NamespaceExpression().Match(text);
        if (!namespaceMatch.Success)
            return;

        var relativePath = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
        var declaredNamespace = namespaceMatch.Groups[1].Value;

        foreach (Match typeMatch in TypeExpression().Matches(text))
            map.TryAdd($"{declaredNamespace}.{typeMatch.Groups[1].Value}", relativePath);
    }

    private static bool IsBuildOutput(string file)
    {
        var separator = Path.DirectorySeparatorChar;
        return file.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || file.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static string ProjectRoot([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && directory.GetFiles("*.csproj").Length == 0)
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException($"No .csproj found above {here}.");
    }

    [GeneratedRegex(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceExpression();

    [GeneratedRegex(@"\b(?:class|interface|record|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex TypeExpression();
}
