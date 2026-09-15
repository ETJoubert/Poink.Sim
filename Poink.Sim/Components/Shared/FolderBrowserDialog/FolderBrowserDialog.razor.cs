using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.FolderBrowserDialog
{
	public partial class FolderBrowserDialog : ComponentBase
	{
		[Parameter, EditorRequired]
		public string StartPath { get; set; } = "";

		[Parameter]
		public EventCallback<string> OnSelect { get; set; }

		[Parameter]
		public EventCallback OnCancel { get; set; }

		private string? _currentPath;
		private string _pathInput = "";
		private IReadOnlyList<FolderEntry> _entries = Array.Empty<FolderEntry>();
		private string? _error;

		protected override void OnInitialized() => Navigate(Directory.Exists(StartPath) ? StartPath : null);

		private void Navigate(string? path)
		{
			_error = null;
			try
			{
				_entries = path is null ? DriveEntries() : FolderEntries(path);
				_currentPath = path;
				_pathInput = path ?? "";
			}
			catch (Exception ex)
			{
				_error = ex.Message;
			}
		}

		private void OnPathInputKeyDown(KeyboardEventArgs e)
		{
			if (e.Key == "Enter")
				NavigateToTypedPath();
		}

		private void NavigateToTypedPath()
		{
			if (Directory.Exists(_pathInput))
				Navigate(_pathInput);
			else
				_error = $"'{_pathInput}' is not a folder this server can see.";
		}

		private void NavigateUp() => Navigate(_currentPath is null ? null : Path.GetDirectoryName(_currentPath));

		private static IReadOnlyList<FolderEntry> DriveEntries() =>
			DriveInfo
				.GetDrives()
				.Where(d => d.IsReady)
				.Select(d => new FolderEntry(d.Name.TrimEnd(Path.DirectorySeparatorChar), d.RootDirectory.FullName, CountZTicksFiles(d.RootDirectory.FullName)))
				.ToList();

		private static IReadOnlyList<FolderEntry> FolderEntries(string path) =>
			Directory
				.GetDirectories(path)
				.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
				.Select(d => new FolderEntry(Path.GetFileName(d), d, CountZTicksFiles(d)))
				.ToList();

		private static int CountZTicksFiles(string path)
		{
			try
			{
				return Directory.EnumerateFiles(path, "*.zticks", SearchOption.TopDirectoryOnly).Take(1000).Count();
			}
			catch
			{
				return 0;
			}
		}

		private Task SelectCurrentAsync() => _currentPath is null ? Task.CompletedTask : OnSelect.InvokeAsync(_currentPath);

		private sealed record FolderEntry(string Name, string FullPath, int ZTicksCount);
	}
}
