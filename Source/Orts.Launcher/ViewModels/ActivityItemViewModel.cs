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

using Orts.Formats.Msts;
using ORTS.Menu;

namespace Orts.Launcher.ViewModels
{
    /// <summary>Wraps an <see cref="ORTS.Menu.Activity"/> for the activity list.</summary>
    public sealed class ActivityItemViewModel
    {
        public ActivityItemViewModel(Activity activity)
        {
            Activity = activity;
        }

        public Activity Activity { get; }

        public string Name => Activity.Name;

        public string Description => string.IsNullOrWhiteSpace(Activity.Description)
            ? "No description."
            : Activity.Description;

        public string Briefing => Activity.Briefing ?? string.Empty;

        public bool HasBriefing => !string.IsNullOrWhiteSpace(Activity.Briefing);

        /// <summary>Planned duration as HH:MM.</summary>
        public string Duration => Activity.Duration?.FormattedDurationTime() ?? "-";

        public string Difficulty => Activity.Difficulty.ToString();

        public string StartTime => Activity.StartTime?.FormattedStartTime() ?? "10:00:00";

        public SeasonType Season => Activity.Season;

        public WeatherType Weather => Activity.Weather;

        /// <summary>True for the synthetic "- Explore Route -" / "+ Explore in Activity Mode +" entries.</summary>
        public bool IsExploreMode => Activity is ExploreActivity;

        /// <summary>Activity file path, or null for the synthetic explore entries.</summary>
        public string? FilePath => Activity.FilePath;

        /// <summary>Stable identity used for selection persistence.</summary>
        public string Key => Activity.FilePath ?? Activity.Name;

        public override string ToString() => Name;
    }
}
