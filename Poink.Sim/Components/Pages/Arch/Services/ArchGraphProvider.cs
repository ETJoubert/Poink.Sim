using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Poink.Sim.Components.Pages.Arch.Models;
using Poink.Sim.Components.Pages.Code.Services;
using Poink.Sim.Trader;

namespace Poink.Sim.Components.Pages.Arch.Services;

internal sealed class ArchGraphProvider : IArchGraphProvider
{
	private readonly IReadOnlyList<ServiceDescriptor> _serviceDescriptors;
	private readonly ICodeSourceIndex _sourceIndex;
	private readonly IXmlDocumentationProvider _xmlDocs;

	public ArchGraphProvider(
		IReadOnlyList<ServiceDescriptor> serviceDescriptors,
		ICodeSourceIndex sourceIndex,
		IXmlDocumentationProvider xmlDocs
	)
	{
		_serviceDescriptors = serviceDescriptors;
		_sourceIndex = sourceIndex;
		_xmlDocs = xmlDocs;
	}

	private sealed record NodeSpec(
		string Id,
		string Title,
		string Area,
		Type Contract,
		Type Implementation,
		string ContractPath,
		string ImplementationPath,
		string Summary
	);

	public ArchGraph Build()
	{
		var specs = BuildSpecs();
		var edges = InferInjectedEdges(specs).Concat(InferProducedEdges(specs)).ToList();
		var columns = ComputeColumns(specs, edges);
		var rows = ComputeRows(specs, columns);
		var nodes = specs.Select(spec => BuildNode(spec, columns[spec.Id], rows[spec.Id], edges)).ToList();
		return new ArchGraph(nodes, edges);
	}

	// Discovered from the live DI container, filtered to registrations reflection can see
	// through; factory- and instance-based ones have no constructor to read and are skipped.
	private IReadOnlyList<NodeSpec> BuildSpecs()
	{
		var discovered = _serviceDescriptors
			.Where(d => d.ImplementationType is not null && IsPoinkSim(d.ImplementationType))
			.GroupBy(d => d.ImplementationType!)
			.Select(BuildDiscoveredSpec)
			.ToList();

		var manual = BuildManualSpecs(discovered);
		return [.. discovered, .. manual];
	}

	private NodeSpec BuildDiscoveredSpec(IGrouping<Type, ServiceDescriptor> group)
	{
		var implementation = group.Key;
		var lifetime = group.First().Lifetime;
		var contract =
			group.FirstOrDefault(d => d.ServiceType.IsInterface && d.ServiceType != implementation)?.ServiceType
			?? implementation;

		return new NodeSpec(
			SlugFor(implementation),
			implementation.Name,
			AreaFor(implementation),
			contract,
			implementation,
			_sourceIndex.PathFor(contract) ?? "path unresolved",
			_sourceIndex.PathFor(implementation) ?? "path unresolved",
			SummaryFor(implementation, $"{lifetime} service")
		);
	}

	// Trader is built per session by TraderFactory.Create, so it never appears as a
	// ServiceDescriptor; it is added here only if discovery did not already surface it.
	private IReadOnlyList<NodeSpec> BuildManualSpecs(IReadOnlyList<NodeSpec> discovered)
	{
		var implementation = typeof(Poink.Sim.Trader.Trader);
		if (discovered.Any(s => s.Implementation == implementation))
			return [];

		var contract = typeof(ITrader);
		return
		[
			new NodeSpec(
				"trader",
				implementation.Name,
				AreaFor(implementation),
				contract,
				implementation,
				_sourceIndex.PathFor(contract) ?? "path unresolved",
				_sourceIndex.PathFor(implementation) ?? "path unresolved",
				SummaryFor(implementation, "Built per session, not a DI service")
			),
		];
	}

	private static bool IsPoinkSim(Type type) =>
		type.Namespace is { } ns && ns.StartsWith("Poink.Sim", StringComparison.Ordinal);

	private static string SlugFor(Type type)
	{
		var builder = new StringBuilder();
		for (var index = 0; index < type.Name.Length; index++)
		{
			if (index > 0 && char.IsUpper(type.Name[index]))
				builder.Append('-');
			builder.Append(char.ToLowerInvariant(type.Name[index]));
		}
		return builder.ToString();
	}

	private static string AreaFor(Type type)
	{
		var ns = type.Namespace ?? "";
		var suffix = ns.StartsWith("Poink.Sim.", StringComparison.Ordinal) ? ns["Poink.Sim.".Length..] : ns;
		var lastDot = suffix.LastIndexOf('.');
		return lastDot < 0 ? (suffix.Length == 0 ? "Core" : suffix) : suffix[(lastDot + 1)..];
	}

	// A chip's description is read from its own class's <summary> — the code carries the
	// information, or this page says plainly that it does not yet.
	private string SummaryFor(Type implementation, string lifetimeLabel)
	{
		var documented = _xmlDocs.SummaryFor(implementation);
		if (documented is { Length: > 0 })
			return documented;

		return $"{lifetimeLabel} in Poink.Sim.{AreaFor(implementation)}. "
			+ $"No <summary> documented on {implementation.Name} yet.";
	}

	// Longest-path-from-sources layering: a chip sits one column right of the
	// furthest-right chip that feeds it, so dependency direction reads left to right.
	private static Dictionary<string, int> ComputeColumns(IReadOnlyList<NodeSpec> specs, IReadOnlyList<ArchEdge> edges)
	{
		var columns = specs.ToDictionary(s => s.Id, _ => 0);
		for (var pass = 0; pass < specs.Count; pass++)
		{
			foreach (var edge in edges)
			{
				var candidate = columns[edge.FromId] + 1;
				if (candidate > columns[edge.ToId])
					columns[edge.ToId] = candidate;
			}
		}
		return columns;
	}

	private static Dictionary<string, int> ComputeRows(
		IReadOnlyList<NodeSpec> specs,
		IReadOnlyDictionary<string, int> columns
	)
	{
		var rows = new Dictionary<string, int>();
		foreach (var group in specs.GroupBy(s => columns[s.Id]))
		{
			var index = 0;
			foreach (var spec in group)
				rows[spec.Id] = index++;
		}
		return rows;
	}

	private static ArchComponentNode BuildNode(NodeSpec spec, int column, int row, IReadOnlyList<ArchEdge> edges)
	{
		var nullability = new NullabilityInfoContext();
		var types =
			spec.Contract == spec.Implementation
				? (IReadOnlyList<ArchTypeRef>)[new ArchTypeRef(spec.Implementation.Name, spec.ImplementationPath)]
				:
				[
					new ArchTypeRef(spec.Implementation.Name, spec.ImplementationPath),
					new ArchTypeRef(spec.Contract.Name, spec.ContractPath),
				];

		return new ArchComponentNode(
			spec.Id,
			spec.Title,
			spec.Area,
			column,
			row,
			types,
			spec.Summary,
			BuildSelfPort(spec, edges),
			BuildInputPorts(spec, nullability, edges),
			BuildMethods(spec, nullability, edges),
			BuildOutputPorts(spec.Contract, nullability)
		);
	}

	// The chip's own identity pin, distinct from the named per-parameter Inputs/Outputs below.
	private static ArchPort BuildSelfPort(NodeSpec spec, IReadOnlyList<ArchEdge> edges)
	{
		var injectsInto = edges
			.Where(e => e.Kind == ArchEdgeKind.Injected && e.FromId == spec.Id)
			.Select(e => e.ToTitle)
			.Distinct()
			.ToList();

		var producedBy = edges.FirstOrDefault(e => e.Kind == ArchEdgeKind.Produces && e.ToId == spec.Id);

		var relationships = new List<string>();
		if (producedBy is not null)
			relationships.Add($"Produced by {producedBy.FromTitle} via {producedBy.Label}()");
		if (injectsInto.Count > 0)
			relationships.Add($"Injected into {string.Join(", ", injectsInto)}");

		var tooltip =
			relationships.Count > 0
				? $"This {spec.Implementation.Name} instance.\n{string.Join("\n", relationships)}"
				: $"This {spec.Implementation.Name} instance. Not referenced elsewhere on this page.";

		var wiredFrom = producedBy?.FromTitle ?? injectsInto.FirstOrDefault();
		return new ArchPort(spec.Title, spec.Implementation.Name, tooltip, WiredFrom: wiredFrom);
	}

	private static ConstructorInfo? PrimaryConstructor(Type implementation) =>
		implementation
			.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
			.OrderByDescending(c => c.GetParameters().Length)
			.FirstOrDefault();

	private static IReadOnlyList<ArchPort> BuildInputPorts(
		NodeSpec spec,
		NullabilityInfoContext nullability,
		IReadOnlyList<ArchEdge> edges
	)
	{
		var constructor = PrimaryConstructor(spec.Implementation);
		if (constructor is null)
			return [];

		return constructor
			.GetParameters()
			.Select(p =>
			{
				var name = p.Name ?? "value";
				var wiredFrom = edges
					.FirstOrDefault(e => e.Kind == ArchEdgeKind.Injected && e.ToId == spec.Id && e.Label == name)
					?.FromTitle;
				var tooltip = wiredFrom is null
					? $"Constructor-injected dependency of {spec.Implementation.Name}, supplied from outside this diagram."
					: $"Constructor-injected dependency of {spec.Implementation.Name}, wired here from {wiredFrom}.";

				return new ArchPort(
					name,
					FriendlyTypeName(p.ParameterType, nullability.Create(p)),
					tooltip,
					WiredFrom: wiredFrom
				);
			})
			.ToList();
	}

	private static IReadOnlyList<ArchMethod> BuildMethods(
		NodeSpec spec,
		NullabilityInfoContext nullability,
		IReadOnlyList<ArchEdge> edges
	) =>
		spec
			.Contract.GetMethods()
			.Where(m => !m.IsSpecialName)
			.Select(m =>
			{
				var produces = edges
					.FirstOrDefault(e => e.Kind == ArchEdgeKind.Produces && e.FromId == spec.Id && e.Label == m.Name)
					?.ToTitle;
				var tooltip = produces is null
					? $"{spec.Contract.Name}.{m.Name} — a public operation on the reflected contract."
					: $"{spec.Contract.Name}.{m.Name} — its return type is reflected as {produces} on this page.";

				return new ArchMethod(FormatSignature(m, nullability), tooltip, produces);
			})
			.ToList();

	private static IReadOnlyList<ArchPort> BuildOutputPorts(Type contract, NullabilityInfoContext nullability)
	{
		var properties = contract
			.GetProperties()
			.Select(p => new ArchPort(
				p.Name,
				FriendlyTypeName(p.PropertyType, nullability.Create(p)),
				$"Read-only state exposed by {contract.Name}."
			));

		var events = contract
			.GetEvents()
			.Select(e => new ArchPort(
				e.Name,
				FriendlyTypeName(e.EventHandlerType!, null),
				$"Event raised by {contract.Name}.",
				IsEvent: true
			));

		return properties.Concat(events).ToList();
	}

	private static IEnumerable<ArchEdge> InferInjectedEdges(IReadOnlyList<NodeSpec> specs)
	{
		foreach (var target in specs)
		{
			var constructor = PrimaryConstructor(target.Implementation);
			if (constructor is null)
				continue;

			foreach (var parameter in constructor.GetParameters())
			{
				var elementType = UnwrapCollection(parameter.ParameterType);
				var isCollection = elementType != parameter.ParameterType;

				// A collection parameter can be fed by every matching chip at once; a
				// single-value parameter still resolves to at most one source.
				IEnumerable<NodeSpec> sources =
					isCollection ? FindSpecs(specs, elementType, target.Id)
					: FindSpec(specs, elementType, target.Id) is { } single ? [single]
					: [];

				foreach (var source in sources)
					yield return new ArchEdge(
						source.Id,
						source.Title,
						target.Id,
						target.Title,
						parameter.Name ?? elementType.Name,
						ArchEdgeKind.Injected
					);
			}
		}
	}

	private static IEnumerable<ArchEdge> InferProducedEdges(IReadOnlyList<NodeSpec> specs)
	{
		foreach (var source in specs)
		{
			foreach (var method in source.Contract.GetMethods().Where(m => !m.IsSpecialName))
			{
				var returnType = UnwrapCollection(method.ReturnType);
				var target = FindSpec(specs, returnType, source.Id);
				if (target is not null)
					yield return new ArchEdge(
						source.Id,
						source.Title,
						target.Id,
						target.Title,
						method.Name,
						ArchEdgeKind.Produces
					);
			}
		}
	}

	private static NodeSpec? FindSpec(IReadOnlyList<NodeSpec> specs, Type type, string excludeId) =>
		FindSpecs(specs, type, excludeId).FirstOrDefault();

	private static IEnumerable<NodeSpec> FindSpecs(IReadOnlyList<NodeSpec> specs, Type type, string excludeId) =>
		specs.Where(s =>
			s.Id != excludeId
			&& (
				s.Implementation == type
				|| s.Contract == type
				|| (type.IsInterface && type.IsAssignableFrom(s.Implementation))
			)
		);

	private static Type UnwrapCollection(Type type)
	{
		if (!type.IsGenericType)
			return type;

		var definition = type.GetGenericTypeDefinition();
		return
			definition == typeof(IEnumerable<>)
			|| definition == typeof(IReadOnlyList<>)
			|| definition == typeof(IReadOnlyCollection<>)
			|| definition == typeof(List<>)
			? type.GetGenericArguments()[0]
			: type;
	}

	private static string FormatSignature(MethodInfo method, NullabilityInfoContext nullability)
	{
		var parameters = method
			.GetParameters()
			.Select(p =>
			{
				var isByRef = p.ParameterType.IsByRef;
				var elementType = isByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
				var prefix =
					p.IsOut ? "out "
					: isByRef ? "ref "
					: "";
				var typeName = FriendlyTypeName(elementType, nullability.Create(p));
				var defaultSuffix = p.HasDefaultValue ? $" = {FormatDefault(p.DefaultValue)}" : "";
				return $"{prefix}{typeName} {p.Name}{defaultSuffix}";
			});

		var returnType = FriendlyTypeName(method.ReturnType, nullability.Create(method.ReturnParameter));
		return $"{returnType} {method.Name}({string.Join(", ", parameters)})";
	}

	private static string FormatDefault(object? value) =>
		value switch
		{
			null => "null",
			bool b => b ? "true" : "false",
			string s => $"\"{s}\"",
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? "null",
		};

	private static string FriendlyTypeName(Type type, NullabilityInfo? nullability)
	{
		var underlying = Nullable.GetUnderlyingType(type);
		if (underlying is not null)
			return $"{FriendlyTypeName(underlying, null)}?";

		string name;
		if (type.IsGenericType)
		{
			var backtick = type.Name.IndexOf('`');
			var genericName = backtick < 0 ? type.Name : type.Name[..backtick];
			var arguments = type.GetGenericArguments();
			var nullabilityArguments = nullability?.GenericTypeArguments;
			var formattedArguments = arguments.Select(
				(argument, index) =>
					FriendlyTypeName(
						argument,
						nullabilityArguments is { Length: > 0 } && index < nullabilityArguments.Length
							? nullabilityArguments[index]
							: null
					)
			);
			name = $"{genericName}<{string.Join(", ", formattedArguments)}>";
		}
		else
		{
			name = AliasFor(type) ?? type.Name;
		}

		var nullableSuffix = !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? "?" : "";
		return name + nullableSuffix;
	}

	private static string? AliasFor(Type type)
	{
		if (type == typeof(string))
			return "string";
		if (type == typeof(bool))
			return "bool";
		if (type == typeof(int))
			return "int";
		if (type == typeof(long))
			return "long";
		if (type == typeof(double))
			return "double";
		if (type == typeof(float))
			return "float";
		if (type == typeof(object))
			return "object";
		if (type == typeof(void))
			return "void";
		return null;
	}
}
