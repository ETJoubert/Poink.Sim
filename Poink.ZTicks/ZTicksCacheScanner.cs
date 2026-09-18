using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Poink.ZTicks;

/// <summary>Finds .zticks day files under a cache path, ordered ascending by the date in their filename.</summary>
public static class ZTicksCacheScanner
{
	private const string DateFormat = "yyyyMMdd";

	public static IReadOnlyList<string> FindFiles(string cachePath, DateOnly? from, DateOnly? to)
	{
		if (File.Exists(cachePath))
			return [cachePath];

		if (!Directory.Exists(cachePath))
			throw new FileNotFoundException($"Cache path not found: {cachePath}");

		return Directory
			.EnumerateFiles(cachePath, "*.zticks", SearchOption.TopDirectoryOnly)
			.Select(path => (Path: path, Date: TryParseFileDate(path)))
			.Where(f => f.Date is null || IsInRange(f.Date.Value, from, to))
			.OrderBy(f => f.Date ?? DateOnly.MinValue)
			.Select(f => f.Path)
			.ToList();
	}

	public static DateOnly? TryParseFileDate(string path)
	{
		var stem = Path.GetFileNameWithoutExtension(path);
		return DateOnly.TryParseExact(stem, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
			? date
			: null;
	}

	/// <summary>Every date found under a cache path, for a diff against stored coverage -- no file content is read.</summary>
	public static IReadOnlyList<DateOnly> FindDates(string cachePath) =>
		FindFiles(cachePath, from: null, to: null)
			.Select(TryParseFileDate)
			.Where(date => date is not null)
			.Select(date => date!.Value)
			.Distinct()
			.OrderBy(date => date)
			.ToList();

	private static bool IsInRange(DateOnly date, DateOnly? from, DateOnly? to) =>
		(from is null || date >= from.Value) && (to is null || date <= to.Value);
}
