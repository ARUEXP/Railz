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

namespace Orts.Launcher.ViewModels
{
    /// <summary>One choice in a pill group (time of day, weather, season).</summary>
    public sealed class OptionItemViewModel
    {
        public OptionItemViewModel(string icon, string label, object value)
        {
            Icon = icon;
            Label = label;
            Value = value;
        }

        public string Icon { get; }

        public string Label { get; }

        /// <summary>The value this choice stands for (a time string, or a season/weather enum).</summary>
        public object Value { get; }

        public override string ToString() => Label;
    }
}
