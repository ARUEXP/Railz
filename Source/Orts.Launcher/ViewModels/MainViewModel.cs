// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using ORTS.Menu;
using ORTS.Settings;

namespace Orts.Launcher.ViewModels
{
    /// <summary>
    /// Drives folder/route discovery for the launcher. All disk and parsing work runs
    /// on background threads; the observable collections are only touched on the UI thread.
    /// </summary>
    public sealed class MainViewModel : ViewModelBase
    {
        UserSettings? _settings;
        bool _suppressFolderLoad;

        public MainViewModel()
        {
            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = o => MatchesSearch((RouteItemViewModel)o);
            Routes.CollectionChanged += (_, __) => OnPropertyChanged(nameof(EmptyStateMessage));
        }

        public ObservableCollection<FolderItemViewModel> Folders { get; } = new ObservableCollection<FolderItemViewModel>();

        public ObservableCollection<RouteItemViewModel> Routes { get; } = new ObservableCollection<RouteItemViewModel>();

        /// <summary>Searchable/filterable view over <see cref="Routes"/>.</summary>
        public ICollectionView RoutesView { get; }

        FolderItemViewModel? _selectedFolder;
        public FolderItemViewModel? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (!SetProperty(ref _selectedFolder, value))
                    return;

                PersistSelection();

                if (!_suppressFolderLoad && value != null && _settings != null)
                    _ = LoadRoutesAsync(value);
            }
        }

        RouteItemViewModel? _selectedRoute;
        public RouteItemViewModel? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                if (SetProperty(ref _selectedRoute, value))
                {
                    PersistSelection();
                    OnPropertyChanged(nameof(ShowEmptyState));
                }
            }
        }

        bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(CanChangeFolder));
                    OnPropertyChanged(nameof(ShowEmptyState));
                }
            }
        }

        /// <summary>False while a scan is running, so the profile picker can't be changed mid-load.</summary>
        public bool CanChangeFolder => !_isLoading;

        /// <summary>True when the centre pane should show the empty-state hint instead of route details.</summary>
        public bool ShowEmptyState => !_isLoading && _selectedRoute == null;

        public string EmptyStateMessage => Routes.Count == 0
            ? "No routes found. Select an MSTS / Open Rails root folder (containing ROUTES and TRAINS directories) to begin."
            : "Select a route from the list to see its details.";

        string _statusMessage = "Ready.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                    RoutesView.Refresh();
            }
        }

        public async Task LoadFoldersAsync(string? preferredFolderPath = null)
        {
            IsLoading = true;
            StatusMessage = "Locating installation profiles\u2026";

            try
            {
                _settings = await Task.Run(() => new UserSettings(Array.Empty<string>()));

                var folders = await Task.Run(() => Folder.GetFolders(_settings)
                    .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

                Folders.Clear();
                foreach (var folder in folders)
                    Folders.Add(new FolderItemViewModel(folder));

                var lastFolder = LastSelection(UserSettings.Menu_SelectionIndex.Folder);
                var lastRoute = LastSelection(UserSettings.Menu_SelectionIndex.Route);
                var match = Folders.FirstOrDefault(f => string.Equals(f.Path, preferredFolderPath, StringComparison.OrdinalIgnoreCase))
                    ?? Folders.FirstOrDefault(f => string.Equals(f.Path, lastFolder, StringComparison.OrdinalIgnoreCase))
                    ?? Folders.FirstOrDefault();

                _suppressFolderLoad = true;
                SelectedFolder = match;
                _suppressFolderLoad = false;

                if (match == null)
                {
                    StatusMessage = "No installation profiles found.";
                    IsLoading = false;
                }
                else
                {
                    // lastRoute is captured before the folder assignment overwrites the stored selection.
                    await LoadRoutesAsync(match, lastRoute);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Startup scan failed: " + ex.Message;
                IsLoading = false;
            }
        }

        public async Task LoadRoutesAsync(FolderItemViewModel folder, string? preferredRoutePath = null)
        {
            IsLoading = true;
            StatusMessage = $"Scanning routes in \u201c{folder.Name}\u201d\u2026";
            Routes.Clear();

            try
            {
                var routes = await Task.Run(() => Route.GetRoutes(folder.Folder)
                    .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

                foreach (var route in routes)
                    Routes.Add(new RouteItemViewModel(route, folder.Folder));

                // Counts and hero images touch disk and parse content, so keep them off the UI thread.
                var loaded = Routes.ToList();
                await Task.Run(() =>
                {
                    foreach (var item in loaded)
                        item.LoadDetails();
                });

                StatusMessage = Routes.Count == 0
                    ? "No routes found in this profile."
                    : $"{Routes.Count} route(s) discovered.";

                var lastRoute = preferredRoutePath ?? LastSelection(UserSettings.Menu_SelectionIndex.Route);
                SelectedRoute = Routes.FirstOrDefault(r => string.Equals(r.FolderPath, lastRoute, StringComparison.OrdinalIgnoreCase))
                    ?? Routes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                StatusMessage = "Route scan failed: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Registers a new installation profile (an MSTS / Open Rails root folder), persists it and reloads.
        /// </summary>
        public async Task AddFolderAsync(string path)
        {
            if (_settings == null || string.IsNullOrWhiteSpace(path))
                return;

            path = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var settings = _settings;
            var name = UniqueFolderName(path);

            var folders = Folders.Select(f => f.Folder).ToList();
            folders.Add(new Folder(name, path));

            StatusMessage = $"Adding profile \u201c{name}\u201d\u2026";
            IsLoading = true;

            try
            {
                await Task.Run(() =>
                {
                    Folder.SetFolders(settings, folders);
                    settings.Save();
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "Could not add the folder: " + ex.Message;
                IsLoading = false;
                return;
            }

            await LoadFoldersAsync(path);
        }

        /// <summary>Profile names are dictionary keys, so a used name gets a numeric suffix.</summary>
        string UniqueFolderName(string path)
        {
            var existing = _settings!.Folders.Folders;
            var baseName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(baseName))
                baseName = path;

            if (!existing.TryGetValue(baseName, out var current) || string.Equals(current, path, StringComparison.OrdinalIgnoreCase))
                return baseName;

            for (var i = 2; ; i++)
            {
                var candidate = $"{baseName} ({i})";
                if (!existing.ContainsKey(candidate))
                    return candidate;
            }
        }

        bool MatchesSearch(RouteItemViewModel route)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return (route.Name?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                || (route.Description?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        string? LastSelection(UserSettings.Menu_SelectionIndex index)
        {
            var selection = _settings?.Menu_Selection;
            var i = (int)index;
            return selection != null && selection.Length > i ? selection[i] : null;
        }

        void PersistSelection()
        {
            if (_settings == null)
                return;

            var current = _settings.Menu_Selection ?? Array.Empty<string>();
            var updated = new string[Math.Max(current.Length, 2)];
            Array.Copy(current, updated, current.Length);
            updated[(int)UserSettings.Menu_SelectionIndex.Folder] = _selectedFolder?.Path ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Route] = _selectedRoute?.FolderPath ?? string.Empty;
            _settings.Menu_Selection = updated;

            var settings = _settings;
            // Persist off the UI thread; selections change infrequently and writes are idempotent.
            _ = Task.Run(() =>
            {
                try
                {
                    settings.Save();
                }
                catch
                {
                    // A failed settings write must never break the launcher.
                }
            });
        }
    }
}
