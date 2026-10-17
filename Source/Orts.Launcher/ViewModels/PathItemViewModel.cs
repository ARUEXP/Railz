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

using ORTS.Menu;

namespace Orts.Launcher.ViewModels
{
    /// <summary>Wraps an <see cref="ORTS.Menu.Path"/> (a .pat file) for the path selector.</summary>
    public sealed class PathItemViewModel
    {
        public PathItemViewModel(Path path)
        {
            Path = path;
        }

        public Path Path { get; }

        public string Name => Path.Name;

        /// <summary>"From A → To B" when the .pat could be read, otherwise the file's own name.</summary>
        public string Label
        {
            get
            {
                var start = LoaderText.Friendly(Path.Start, string.Empty);
                var end = LoaderText.Friendly(Path.End, string.Empty);

                return start.Length > 0 && end.Length > 0
                    ? $"From {start} \u2192 To {end}"
                    : LoaderText.Friendly(Path.Name, "Starting location", FilePath);
            }
        }

        public string FilePath => Path.FilePath;
    }
}
