using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Poink.Sim.Tools.ZTicksImport.Storage;

/// <summary>Resolves the PoinkMarketData connection string: explicit override, then environment, then appsettings near the working directory.</summary>
internal static class ConnectionStringResolver
{
	private const string EnvironmentVariableName = "POINK_MARKETDATA_CONNECTION";
	private const string ConnectionKey = "PoinkMarketData";

	public static string Resolve(string? explicitConnectionString)
	{
		if (!string.IsNullOrWhiteSpace(explicitConnectionString))
			return explicitConnectionString;

		var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
		if (!string.IsNullOrWhiteSpace(fromEnvironment))
			return fromEnvironment;

		var candidateDirectories = CandidatePoinkSimDirectories().ToList();
		foreach (var directory in candidateDirectories)
		{
			var fromSettings =
				ReadConnectionString(Path.Combine(directory, "appsettings.Secrets.json"))
				?? ReadConnectionString(Path.Combine(directory, "appsettings.json"));

			if (!string.IsNullOrWhiteSpace(fromSettings))
				return fromSettings;
		}

		throw new InvalidOperationException(
			$"Could not resolve a '{ConnectionKey}' connection string. Pass --connection-string, "
				+ $"set {EnvironmentVariableName}, or run this tool from a location where one of "
				+ $"[{string.Join(", ", candidateDirectories)}] contains an appsettings.json with "
				+ $"ConnectionStrings:{ConnectionKey}."
		);
	}

	private static IEnumerable<string> CandidatePoinkSimDirectories()
	{
		var cwd = Directory.GetCurrentDirectory();
		yield return Path.Combine(cwd, "Poink.Sim");
		yield return Path.Combine(cwd, "..", "Poink.Sim");
		yield return cwd;
	}

	private static string? ReadConnectionString(string settingsFilePath)
	{
		if (!File.Exists(settingsFilePath))
			return null;

		using var document = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
		return document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
				&& connectionStrings.TryGetProperty(ConnectionKey, out var value)
			? value.GetString()
			: null;
	}
}
