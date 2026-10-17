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
using Orts.Launcher.Services;
using MenuPath = ORTS.Menu.Path;
using SeasonType = Orts.Formats.Msts.SeasonType;
using WeatherType = Orts.Formats.Msts.WeatherType;

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
        bool _suppressRouteDetails;

        /// <summary>Which selection panel the route detail pane shows.</summary>
        public enum LauncherMode { QuickDrive, Activity, Timetable }

        public MainViewModel()
        {
            // Quick Drive is the landing tab: its train and starting location are auto-selected,
            // so picking a route is already enough to drive.
            _timeOption = TimeOptions[1];       // Noon
            _seasonOption = SeasonOptions[1];   // Summer
            _weatherOption = WeatherOptions[0]; // Sunny

            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = o => MatchesSearch((RouteItemViewModel)o);
            Routes.CollectionChanged += (_, __) => OnPropertyChanged(nameof(EmptyStateMessage));

            ConsistsView = CollectionViewSource.GetDefaultView(Consists);
            ConsistsView.Filter = o => MatchesConsistSearch((ConsistItemViewModel)o);
        }

        public ObservableCollection<FolderItemViewModel> Folders { get; } = new ObservableCollection<FolderItemViewModel>();

        public ObservableCollection<RouteItemViewModel> Routes { get; } = new ObservableCollection<RouteItemViewModel>();

        /// <summary>Searchable/filterable view over <see cref="Routes"/>.</summary>
        public ICollectionView RoutesView { get; }

        public ObservableCollection<ActivityItemViewModel> Activities { get; } = new ObservableCollection<ActivityItemViewModel>();

        public ObservableCollection<ConsistItemViewModel> Consists { get; } = new ObservableCollection<ConsistItemViewModel>();

        public ObservableCollection<PathItemViewModel> Paths { get; } = new ObservableCollection<PathItemViewModel>();

        /// <summary>Searchable/filterable view over <see cref="Consists"/>.</summary>
        public ICollectionView ConsistsView { get; }

        ActivityItemViewModel? _selectedActivity;
        public ActivityItemViewModel? SelectedActivity
        {
            get => _selectedActivity;
            set { if (SetProperty(ref _selectedActivity, value)) { PersistSelection(); NotifyLaunchState(); } }
        }

        ConsistItemViewModel? _selectedConsist;
        public ConsistItemViewModel? SelectedConsist
        {
            get => _selectedConsist;
            set { if (SetProperty(ref _selectedConsist, value)) { PersistSelection(); NotifyLaunchState(); } }
        }

        PathItemViewModel? _selectedPath;
        public PathItemViewModel? SelectedPath
        {
            get => _selectedPath;
            set { if (SetProperty(ref _selectedPath, value)) { PersistSelection(); NotifyLaunchState(); } }
        }

        LauncherMode _mode = LauncherMode.QuickDrive;

        public bool IsQuickDriveMode
        {
            get => _mode == LauncherMode.QuickDrive;
            set => SetMode(value, LauncherMode.QuickDrive);
        }

        public bool IsActivityMode
        {
            get => _mode == LauncherMode.Activity;
            set => SetMode(value, LauncherMode.Activity);
        }

        public bool IsTimetableMode
        {
            get => _mode == LauncherMode.Timetable;
            set => SetMode(value, LauncherMode.Timetable);
        }

        void SetMode(bool requested, LauncherMode mode)
        {
            // A segmented button can select a mode, but must never clear the active one.
            if (requested && _mode != mode)
                _mode = mode;

            OnPropertyChanged(nameof(IsQuickDriveMode));
            OnPropertyChanged(nameof(IsActivityMode));
            OnPropertyChanged(nameof(IsTimetableMode));
            NotifyLaunchState();
        }

        // ---- environment pills (time of day, weather, season) ----

        OptionItemViewModel _timeOption;
        OptionItemViewModel _seasonOption;
        OptionItemViewModel _weatherOption;

        public IReadOnlyList<OptionItemViewModel> TimeOptions { get; } = new[]
        {
            new OptionItemViewModel("\U0001F305", "Morning", "7:00"),
            new OptionItemViewModel("\u2600\uFE0F", "Noon", "12:00"),
            new OptionItemViewModel("\U0001F319", "Night", "21:00"),
        };

        public IReadOnlyList<OptionItemViewModel> WeatherOptions { get; } = new[]
        {
            new OptionItemViewModel("\u2600\uFE0F", "Sunny", WeatherType.Clear),
            new OptionItemViewModel("\U0001F327\uFE0F", "Rain", WeatherType.Rain),
            new OptionItemViewModel("\u2744\uFE0F", "Snow", WeatherType.Snow),
        };

        public IReadOnlyList<OptionItemViewModel> SeasonOptions { get; } = new[]
        {
            new OptionItemViewModel("\U0001F338", "Spring", SeasonType.Spring),
            new OptionItemViewModel("\u2600\uFE0F", "Summer", SeasonType.Summer),
            new OptionItemViewModel("\U0001F342", "Autumn", SeasonType.Autumn),
            new OptionItemViewModel("\u2744\uFE0F", "Winter", SeasonType.Winter),
        };

        public OptionItemViewModel SelectedTimeOption
        {
            get => _timeOption;
            set { if (value != null && SetProperty(ref _timeOption, value)) { OnPropertyChanged(nameof(SelectedTime)); PersistSelection(); } }
        }

        public OptionItemViewModel SelectedSeasonOption
        {
            get => _seasonOption;
            set { if (value != null && SetProperty(ref _seasonOption, value)) { OnPropertyChanged(nameof(SelectedSeason)); PersistSelection(); } }
        }

        public OptionItemViewModel SelectedWeatherOption
        {
            get => _weatherOption;
            set { if (value != null && SetProperty(ref _weatherOption, value)) { OnPropertyChanged(nameof(SelectedWeather)); PersistSelection(); } }
        }

        /// <summary>Departure time, in the "H:MM" form the simulator parses.</summary>
        public string SelectedTime => (string)_timeOption.Value;

        public SeasonType SelectedSeason => (SeasonType)_seasonOption.Value;

        public WeatherType SelectedWeather => (WeatherType)_weatherOption.Value;

        string _consistSearchText = string.Empty;
        public string ConsistSearchText
        {
            get => _consistSearchText;
            set { if (SetProperty(ref _consistSearchText, value ?? string.Empty)) ConsistsView.Refresh(); }
        }

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
                if (!SetProperty(ref _selectedRoute, value))
                    return;

                PersistSelection();
                OnPropertyChanged(nameof(ShowEmptyState));

                if (_suppressRouteDetails)
                    return;

                if (value != null)
                    _ = LoadRouteDetailsAsync(value);
                else
                    ClearRouteDetails();
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
            ? "No routes found yet. Use the + button to add your game library folder \u2014 the one that contains the ROUTES and TRAINS folders."
            : "Pick a route on the left to get driving.";

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
                _startupSelection = (_settings.Menu_Selection ?? Array.Empty<string>()).ToArray();

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
                var chosen = Routes.FirstOrDefault(r => string.Equals(r.FolderPath, lastRoute, StringComparison.OrdinalIgnoreCase))
                    ?? Routes.FirstOrDefault();

                _suppressRouteDetails = true;
                SelectedRoute = chosen;
                _suppressRouteDetails = false;

                if (chosen != null)
                    await LoadRouteDetailsAsync(chosen);
                else
                    ClearRouteDetails();
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
        /// Loads the activities, paths and consists of a route. All parsing runs on a background thread;
        /// the bound collections are only touched on the UI thread.
        /// </summary>
        public async Task LoadRouteDetailsAsync(RouteItemViewModel route)
        {
            var folder = route.Folder;
            IsLoading = true;
            StatusMessage = $"Loading activities, consists and paths for \u201c{route.Name}\u201d\u2026";

            try
            {
                var loaded = await Task.Run(() =>
                {
                    var activities = Activity.GetActivities(folder, route.Route)
                        .Where(a => !(a is ExploreActivity))   // the synthetic free-drive entries are the Quick Drive tab's job
                        .Select(a => new ActivityItemViewModel(a))
                        .ToList();

                    var paths = MenuPath.GetPaths(route.Route, false)
                        .Select(p => new PathItemViewModel(p))
                        .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    var consists = Consist.GetConsists(folder)
                        .Select(c => new ConsistItemViewModel(c))
                        .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    foreach (var consist in consists)
                        consist.LoadDetails();

                    return (activities, paths, consists);
                });

                // A later selection may have superseded this load while it was running.
                if (!ReferenceEquals(_selectedRoute, route))
                    return;

                SelectedActivity = null;
                SelectedConsist = null;
                SelectedPath = null;
                Replace(Activities, loaded.activities);
                Replace(Paths, loaded.paths);
                Replace(Consists, loaded.consists);

                RestoreRouteDetailSelections();

                StatusMessage = $"{Activities.Count} missions, {Paths.Count} places, {Consists.Count} trains.";
                NotifyLaunchState();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load route details: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        void ClearRouteDetails()
        {
            SelectedActivity = null;
            SelectedConsist = null;
            SelectedPath = null;
            Activities.Clear();
            Paths.Clear();
            Consists.Clear();
            NotifyLaunchState();
        }

        /// <summary>
        /// Re-applies the stored mission / train / place / environment choices for the current route.
        /// Anything stored but no longer present falls back to the first entry, so the launcher always
        /// lands on a usable selection instead of an empty one.
        /// </summary>
        void RestoreRouteDetailSelections()
        {
            var activity = LastSelection(UserSettings.Menu_SelectionIndex.Activity);
            SelectedActivity = Activities.FirstOrDefault(a => string.Equals(a.Key, activity, StringComparison.OrdinalIgnoreCase))
                ?? Activities.FirstOrDefault();

            var consistPath = LastSelection(UserSettings.Menu_SelectionIndex.Consist);
            SelectedConsist = Consists.FirstOrDefault(c => string.Equals(c.FilePath, consistPath, StringComparison.OrdinalIgnoreCase))
                ?? Consists.FirstOrDefault();

            var pathPath = LastSelection(UserSettings.Menu_SelectionIndex.Path);
            SelectedPath = Paths.FirstOrDefault(p => string.Equals(p.FilePath, pathPath, StringComparison.OrdinalIgnoreCase))
                ?? Paths.FirstOrDefault();

            var hour = HourOf(LastSelection(UserSettings.Menu_SelectionIndex.Time));
            var timeOption = TimeOptions.FirstOrDefault(o => HourOf((string)o.Value) == hour);
            if (timeOption != null)
                SelectedTimeOption = timeOption;

            // Enum.TryParse reads both the legacy integer form ("1") and the name form ("Summer").
            var seasonOption = Enum.TryParse<SeasonType>(LastSelection(UserSettings.Menu_SelectionIndex.Season), out var season)
                ? SeasonOptions.FirstOrDefault(o => (SeasonType)o.Value == season)
                : null;
            if (seasonOption != null)
                SelectedSeasonOption = seasonOption;

            var weatherOption = Enum.TryParse<WeatherType>(LastSelection(UserSettings.Menu_SelectionIndex.Weather), out var weather)
                ? WeatherOptions.FirstOrDefault(o => (WeatherType)o.Value == weather)
                : null;
            if (weatherOption != null)
                SelectedWeatherOption = weatherOption;
        }

        static int? HourOf(string? time)
        {
            return int.TryParse((time ?? string.Empty).Split(':')[0], out var hour) ? hour : null;
        }

        static void Replace<T>(ObservableCollection<T> target, IList<T> source)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
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

        // ---- launch hand-off ----

        /// <summary>The RunActivity.exe command line for the current selection, or null when there is nothing to start.</summary>
        public string? LaunchArguments
        {
            get
            {
                if (_mode == LauncherMode.Activity)
                {
                    var activity = _selectedActivity?.FilePath;
                    return string.IsNullOrEmpty(activity) ? null : SimulatorLaunchService.ActivityArguments(activity);
                }

                if (_mode == LauncherMode.QuickDrive)
                {
                    var path = _selectedPath?.FilePath;
                    var consist = _selectedConsist?.FilePath;
                    return string.IsNullOrEmpty(path) || string.IsNullOrEmpty(consist)
                        ? null
                        : SimulatorLaunchService.QuickDriveArguments(path, consist, SelectedTime, SelectedSeason, SelectedWeather);
                }

                return null;
            }
        }

        /// <summary>Plain-English reason the Drive button cannot start anything yet; empty when it can.</summary>
        public string LaunchProblem
        {
            get
            {
                if (_mode == LauncherMode.Activity)
                {
                    return string.IsNullOrEmpty(_selectedActivity?.FilePath)
                        ? "Pick a mission above, then press Start Mission."
                        : string.Empty;
                }

                if (_mode == LauncherMode.QuickDrive)
                {
                    if (string.IsNullOrEmpty(_selectedConsist?.FilePath))
                        return "Select a train above to drive.";
                    if (string.IsNullOrEmpty(_selectedPath?.FilePath))
                        return "Select a starting location above to drive.";
                }

                return string.Empty;
            }
        }

        public string LaunchButtonLabel =>
            _mode == LauncherMode.Activity ? "\u25B6  Start Mission" : "\u25B6  Drive Now";

        void NotifyLaunchState()
        {
            OnPropertyChanged(nameof(LaunchArguments));
            OnPropertyChanged(nameof(LaunchProblem));
            OnPropertyChanged(nameof(LaunchButtonLabel));
        }

        /// <summary>Starts the simulator and returns once it has exited.</summary>
        public async Task LaunchAsync()
        {
            var arguments = LaunchArguments;
            if (arguments == null)
                return;

            try
            {
                StatusMessage = "Starting the simulator\u2026";
                await SimulatorLaunchService.RunAsync(arguments);
                StatusMessage = "The simulator has closed.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Could not start the simulator: " + ex.Message;
            }
        }

        bool MatchesSearch(RouteItemViewModel route)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return (route.Name?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                || (route.Description?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        bool MatchesConsistSearch(ConsistItemViewModel consist)
        {
            if (string.IsNullOrWhiteSpace(ConsistSearchText))
                return true;

            return (consist.Name?.IndexOf(ConsistSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                || (consist.LocomotiveName?.IndexOf(ConsistSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Stored selections as they were when the launcher started.</summary>
        /// <remarks>
        /// PersistSelection writes the current UI state back over Menu_Selection, which happens while a
        /// route is still being restored. Restoring must therefore read the startup values, not the live array.
        /// </remarks>
        string[] _startupSelection = Array.Empty<string>();

        string? LastSelection(UserSettings.Menu_SelectionIndex index)
        {
            var i = (int)index;
            return i < _startupSelection.Length ? _startupSelection[i] : null;
        }

        void PersistSelection()
        {
            if (_settings == null)
                return;

            var current = _settings.Menu_Selection ?? Array.Empty<string>();
            var updated = new string[Math.Max(current.Length, 9)];
            Array.Copy(current, updated, current.Length);
            updated[(int)UserSettings.Menu_SelectionIndex.Folder] = _selectedFolder?.Path ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Route] = _selectedRoute?.FolderPath ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Activity] = _selectedActivity?.Key ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Locomotive] = _selectedConsist?.LocomotivePath ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Consist] = _selectedConsist?.FilePath ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Path] = _selectedPath?.FilePath ?? string.Empty;
            updated[(int)UserSettings.Menu_SelectionIndex.Time] = SelectedTime;
            // Season and weather are stored as their numbers, which is what the legacy Menu reads back.
            updated[(int)UserSettings.Menu_SelectionIndex.Season] = ((int)SelectedSeason).ToString();
            updated[(int)UserSettings.Menu_SelectionIndex.Weather] = ((int)SelectedWeather).ToString();
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
