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

namespace Orts.Launcher.ViewModels
{
    /// <summary>
    /// ORTS.Menu marks content it could not read with a "&lt;load error: X&gt;" style name, and its
    /// placeholder locomotive is literally called "unknown". Those leak file names and loader wording,
    /// so the launcher shows a plain label instead.
    /// </summary>
    static class LoaderText
    {
        public static string Friendly(string? name, string fallback, string? filePath = null)
        {
            var isMarker = string.IsNullOrWhiteSpace(name)
                || (name!.StartsWith("<", StringComparison.Ordinal) && name.EndsWith(">", StringComparison.Ordinal))
                || string.Equals(name, "unknown", StringComparison.OrdinalIgnoreCase);

            if (!isMarker)
                return name!;

            var file = string.IsNullOrEmpty(filePath) ? null : Path.GetFileNameWithoutExtension(filePath);

            // ORTS.Menu's placeholder locomotive lives in a file just called "unknown"; naming it adds nothing.
            if (string.Equals(file, "unknown", StringComparison.OrdinalIgnoreCase))
                file = null;

            return string.IsNullOrEmpty(file) ? fallback : $"{fallback} \u2014 {file}";
        }
    }
}
