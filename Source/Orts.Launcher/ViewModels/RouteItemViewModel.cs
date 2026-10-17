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
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ORTS.Menu;
using MenuPath = ORTS.Menu.Path;

namespace Orts.Launcher.ViewModels
{
    /// <summary>
    /// Wraps an <see cref="ORTS.Menu.Route"/> for the route list and detail pane.
    /// <see cref="LoadDetails"/> performs all disk work and is intended to run on a background thread.
    /// </summary>
    public sealed class RouteItemViewModel : ViewModelBase
    {
        /// <summary>
        /// MSTS/Open Rails content has no standard route artwork, so common preview
        /// file names are probed and a themed banner is used when none is present.
        /// </summary>
        static readonly string[] PreviewImageNames =
        {
            "route.jpg", "route.jpeg", "route.png",
            "screenshot.jpg", "screenshot.jpeg", "screenshot.png",
            "preview.jpg", "preview.jpeg", "preview.png",
        };

        readonly ORTS.Menu.Folder _folder;

        public RouteItemViewModel(Route route, ORTS.Menu.Folder folder)
        {
            Route = route;
            _folder = folder;
        }

        public Route Route { get; }

        public string Name => Route.Name;

        public string Description => string.IsNullOrWhiteSpace(Route.Description)
            ? "No description is available for this route."
            : Route.Description;

        /// <summary>Full path of the route folder on disk.</summary>
        public string FolderPath => Route.Path;

        public string? RouteID => Route.RouteID;

        int? _pathCount;
        public int? PathCount
        {
            get => _pathCount;
            private set => SetProperty(ref _pathCount, value);
        }

        int? _activityCount;
        public int? ActivityCount
        {
            get => _activityCount;
            private set => SetProperty(ref _activityCount, value);
        }

        ImageSource? _heroImage;
        public ImageSource? HeroImage
        {
            get => _heroImage;
            private set
            {
                if (SetProperty(ref _heroImage, value))
                    OnPropertyChanged(nameof(HasHeroImage));
            }
        }

        public bool HasHeroImage => _heroImage != null;

        /// <summary>
        /// Discovers path/activity counts and hero artwork. Safe to call from a background thread.
        /// </summary>
        public void LoadDetails()
        {
            try
            {
                PathCount = MenuPath.GetPaths(Route, false).Count;
            }
            catch
            {
                PathCount = null;
            }

            try
            {
                // Exclude the two synthetic explore entries so the count reflects real activities on disk.
                ActivityCount = Activity.GetActivities(_folder, Route).Count(a => a.FilePath != null);
            }
            catch
            {
                ActivityCount = null;
            }

            try
            {
                HeroImage = LoadHeroImage();
            }
            catch
            {
                HeroImage = null;
            }
        }

        ImageSource? LoadHeroImage()
        {
            foreach (var fileName in PreviewImageNames)
            {
                var candidate = System.IO.Path.Combine(Route.Path, fileName);
                if (!File.Exists(candidate))
                    continue;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;            // don't keep the file locked
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.DecodePixelWidth = 640;                            // bound memory for the banner
                bitmap.UriSource = new Uri(candidate);
                bitmap.EndInit();
                bitmap.Freeze();                                          // safe to hand to the UI thread
                return bitmap;
            }

            return null;
        }
    }
}
