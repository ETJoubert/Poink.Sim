using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Calls.Models;

namespace Poink.Sim.Components.Pages.Calls.Services;

/// <summary>
/// Builds a method-level call graph for Poink.Sim's own source by parsing it with Roslyn and
/// resolving invocations through a manually assembled CSharpCompilation — no MSBuildWorkspace,
/// since this runs inside the already-live app process and the app ships without the .NET SDK.
/// </summary>
internal sealed class CallGraphIndex : ICallGraphIndex
{
	private readonly ILogger<CallGraphIndex> _logger;
	private readonly Lazy<Task<CallGraphIndexResult>> _result;

	private static readonly SymbolDisplayFormat SignatureFormat = new(
		memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
	);

	public CallGraphIndex(ILogger<CallGraphIndex> logger)
	{
		_logger = logger;
		_result = new Lazy<Task<CallGraphIndexResult>>(() => Task.Run(Build));
	}

	public Task<CallGraphIndexResult> GetAsync() => _result.Value;

	private CallGraphIndexResult Build()
	{
		var stopwatch = Stopwatch.StartNew();
		var root = ProjectRoot();
		var trees = ParseSourceFiles(root, out var fileCount);
		var compilation = CSharpCompilation.Create(
			"Poink.Sim.CallGraph",
			trees,
			LoadReferences(out var failedReferenceCount),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		if (failedReferenceCount > 0)
			_logger.LogWarning(
				"Call graph index: {FailedCount} referenced assemblies could not be loaded and are excluded from symbol resolution",
				failedReferenceCount
			);

		var types = new Dictionary<INamedTypeSymbol, TypeBuilder>(SymbolEqualityComparer.Default);
		var methods = new Dictionary<IMethodSymbol, MethodBuilder>(SymbolEqualityComparer.Default);

		// Each step looks up entries the previous one built — types before constructors,
		// methods before call resolution.
		var diagnosticCount = CollectDeclaredTypes(trees, compilation, root, types);
		RegisterAllConstructors(types, methods);
		CollectDeclaredMethods(trees, compilation, root, methods);
		ResolveAllCalls(methods, compilation);

		var resultTypes = BuildTypes(types.Values, methods.Values);
		stopwatch.Stop();

		var stats = new CallGraphStats(
			resultTypes.Count,
			methods.Count,
			methods.Values.Sum(m => m.Outgoing.Count),
			stopwatch.Elapsed,
			diagnosticCount
		);

		_logger.LogInformation(
			"Call graph index built: {TypeCount} types, {MethodCount} methods, {EdgeCount} call edges across "
				+ "{FileCount} source files under {Root} in {ElapsedMs}ms ({DiagnosticCount} semantic diagnostics)",
			stats.TypeCount,
			stats.MethodCount,
			stats.EdgeCount,
			fileCount,
			root,
			stopwatch.ElapsedMilliseconds,
			diagnosticCount
		);

		return new CallGraphIndexResult(resultTypes, stats);
	}

	private static IReadOnlyList<SyntaxTree> ParseSourceFiles(string root, out int fileCount)
	{
		var options = new CSharpParseOptions(LanguageVersion.Latest);
		var trees = new List<SyntaxTree>();
		fileCount = 0;

		foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
		{
			if (IsBuildOutput(file))
				continue;

			fileCount++;
			var text = File.ReadAllText(file);
			trees.Add(CSharpSyntaxTree.ParseText(text, options, path: file));
		}

		return trees;
	}

	private static int CollectDeclaredTypes(
		IReadOnlyList<SyntaxTree> trees,
		Compilation compilation,
		string root,
		Dictionary<INamedTypeSymbol, TypeBuilder> types
	)
	{
		var diagnosticCount = 0;
		foreach (var tree in trees)
		{
			var model = compilation.GetSemanticModel(tree);
			diagnosticCount += model.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
			CollectTypes(tree, model, root, types);
		}

		return diagnosticCount;
	}

	private static void RegisterAllConstructors(
		Dictionary<INamedTypeSymbol, TypeBuilder> types,
		Dictionary<IMethodSymbol, MethodBuilder> methods
	)
	{
		foreach (var entry in types)
			RegisterConstructors(entry.Key, entry.Value, methods);
	}

	private static void CollectDeclaredMethods(
		IReadOnlyList<SyntaxTree> trees,
		Compilation compilation,
		string root,
		Dictionary<IMethodSymbol, MethodBuilder> methods
	)
	{
		foreach (var tree in trees)
			CollectMethods(tree, compilation.GetSemanticModel(tree), root, methods);
	}

	private static void ResolveAllCalls(Dictionary<IMethodSymbol, MethodBuilder> methods, Compilation compilation)
	{
		foreach (var method in methods.Values.Where(m => m.Node is not null))
			ResolveCalls(method, compilation.GetSemanticModel(method.Tree!), methods);
	}

	// Forces assemblies not yet JIT-touched (e.g. TorchSharp, EF Core) to load, so the
	// reference set below does not silently miss them.
	private static List<MetadataReference> LoadReferences(out int failedCount)
	{
		failedCount = 0;
		foreach (var name in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
		{
			try
			{
				Assembly.Load(name);
			}
			catch (Exception)
			{
				failedCount++;
			}
		}

		var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var references = new List<MetadataReference>();

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
				continue;

			if (seenLocations.Add(assembly.Location))
				references.Add(MetadataReference.CreateFromFile(assembly.Location));
		}

		return references;
	}

	// Registers every declared type, not just ones with a method — a record/interface/enum
	// needs to appear too, so the page can tell a Model apart from a Service.
	private static void CollectTypes(
		SyntaxTree tree,
		SemanticModel model,
		string root,
		Dictionary<INamedTypeSymbol, TypeBuilder> types
	)
	{
		foreach (var node in tree.GetRoot().DescendantNodes())
		{
			if (
				node
				is not (
					ClassDeclarationSyntax
					or InterfaceDeclarationSyntax
					or RecordDeclarationSyntax
					or StructDeclarationSyntax
					or EnumDeclarationSyntax
				)
			)
				continue;

			if (
				model.GetDeclaredSymbol(node) is not INamedTypeSymbol symbol
				|| !IsPoinkSim(symbol)
				|| types.ContainsKey(symbol)
			)
				continue;

			var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

			types[symbol] = new TypeBuilder
			{
				Id = TypeId(symbol),
				Name = symbol.Name,
				Namespace = symbol.ContainingNamespace.ToDisplayString(),
				FilePath = RelativePath(root, tree.FilePath),
				Line = line,
				Kind = StructuralKind(symbol),
			};
		}
	}

	// Includes compiler-synthesized constructors (records, default no-arg ones) — those have no
	// ConstructorDeclarationSyntax for CollectMethods to find, so "new Foo()" would not resolve.
	private static void RegisterConstructors(
		INamedTypeSymbol typeSymbol,
		TypeBuilder typeBuilder,
		Dictionary<IMethodSymbol, MethodBuilder> methods
	)
	{
		foreach (var constructor in typeSymbol.Constructors)
		{
			methods[constructor] = new MethodBuilder
			{
				Id = MethodId(constructor),
				Signature = constructor.ToDisplayString(SignatureFormat),
				TypeId = typeBuilder.Id,
				TypeName = typeBuilder.Name,
				FilePath = typeBuilder.FilePath,
				Line = typeBuilder.Line,
				Node = null,
				Tree = null,
			};
		}
	}

	private static void CollectMethods(
		SyntaxTree tree,
		SemanticModel model,
		string root,
		Dictionary<IMethodSymbol, MethodBuilder> methods
	)
	{
		foreach (var node in tree.GetRoot().DescendantNodes())
		{
			if (node is not MethodDeclarationSyntax and not ConstructorDeclarationSyntax)
				continue;

			if (model.GetDeclaredSymbol(node) is not IMethodSymbol symbol || !IsPoinkSim(symbol.ContainingType))
				continue;

			var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

			methods[symbol] = new MethodBuilder
			{
				Id = MethodId(symbol),
				Signature = symbol.ToDisplayString(SignatureFormat),
				TypeId = TypeId(symbol.ContainingType),
				TypeName = symbol.ContainingType.Name,
				FilePath = RelativePath(root, tree.FilePath),
				Line = line,
				Node = node,
				Tree = tree,
			};
		}
	}

	private static void ResolveCalls(
		MethodBuilder method,
		SemanticModel model,
		Dictionary<IMethodSymbol, MethodBuilder> methods
	)
	{
		foreach (var node in method.Node!.DescendantNodes())
		{
			if (node is not InvocationExpressionSyntax and not ObjectCreationExpressionSyntax)
				continue;

			if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol targetSymbol)
				continue;

			if (!methods.TryGetValue(targetSymbol.OriginalDefinition, out var target))
				continue;

			var line = method.Tree!.GetLineSpan(node.Span).StartLinePosition.Line + 1;
			var site = new CallLocation(method.FilePath, line);

			method.Outgoing.Add(new CallGraphEdge(target.Id, target.Signature, target.TypeName, site));
			target.Incoming.Add(new CallGraphEdge(method.Id, method.Signature, method.TypeName, site));
		}
	}

	private static IReadOnlyList<CallGraphType> BuildTypes(
		IEnumerable<TypeBuilder> types,
		IEnumerable<MethodBuilder> methods
	)
	{
		var methodsByType = methods.ToLookup(m => m.TypeId);

		return types
			.Select(t => new CallGraphType(
				t.Id,
				t.Name,
				t.Namespace,
				t.FilePath,
				t.Kind,
				methodsByType[t.Id]
					.Select(m => new CallGraphMethod(
						m.Id,
						m.Signature,
						new CallLocation(m.FilePath, m.Line),
						m.Outgoing,
						m.Incoming
					))
					.OrderBy(m => m.Signature, StringComparer.Ordinal)
					.ToList()
			))
			.OrderBy(t => t.Name, StringComparer.Ordinal)
			.ToList();
	}

	private static string StructuralKind(INamedTypeSymbol symbol) =>
		symbol.TypeKind switch
		{
			TypeKind.Interface => "Interface",
			TypeKind.Enum => "Enum",
			_ when symbol.IsRecord => "Record",
			TypeKind.Struct => "Struct",
			TypeKind.Class => "Class",
			_ => symbol.TypeKind.ToString(),
		};

	private static bool IsPoinkSim(INamedTypeSymbol? type) =>
		type is not null
		&& type.ContainingNamespace.ToDisplayString().StartsWith("Poink.Sim", StringComparison.Ordinal);

	private static string TypeId(INamedTypeSymbol type) => $"{type.ContainingNamespace.ToDisplayString()}.{type.Name}";

	private static string MethodId(IMethodSymbol symbol) =>
		$"{TypeId(symbol.ContainingType)}.{symbol.ToDisplayString(SignatureFormat)}";

	private static string RelativePath(string root, string file) =>
		Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');

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

	private sealed class TypeBuilder
	{
		public required string Id { get; init; }
		public required string Name { get; init; }
		public required string Namespace { get; init; }
		public required string FilePath { get; init; }
		public required int Line { get; init; }
		public required string Kind { get; init; }
	}

	private sealed class MethodBuilder
	{
		public required string Id { get; init; }
		public required string Signature { get; init; }
		public required string TypeId { get; init; }
		public required string TypeName { get; init; }
		public required string FilePath { get; init; }
		public required int Line { get; init; }
		public SyntaxNode? Node { get; init; }
		public SyntaxTree? Tree { get; init; }
		public List<CallGraphEdge> Outgoing { get; } = [];
		public List<CallGraphEdge> Incoming { get; } = [];
	}
}
