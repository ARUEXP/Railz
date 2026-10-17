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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ORTS.Common;
using SeasonType = Orts.Formats.Msts.SeasonType;
using WeatherType = Orts.Formats.Msts.WeatherType;

namespace Orts.Launcher.Services
{
    /// <summary>
    /// Builds the RunActivity.exe command line and starts the simulator.
    /// The arguments are exactly those the legacy Menu produces (Menu/Program.cs) and those
    /// GameStateRunActivity.InitSimulator consumes, so both launchers stay interchangeable.
    /// </summary>
    public static class SimulatorLaunchService
    {
        public static string RunActivityPath => Path.Combine(ApplicationInfo.ProcessDirectory, "RunActivity.exe");

        /// <summary>A mission: <c>-start -activity "&lt;abs .act&gt;"</c></summary>
        public static string ActivityArguments(string activityFilePath) =>
            $"-start -activity \"{activityFilePath}\"";

        /// <summary>
        /// A free drive: <c>-start -explorer "&lt;abs .pat&gt;" "&lt;abs .con&gt;" &lt;H:MM&gt; &lt;season&gt; &lt;weather&gt;</c>.
        /// Season and weather are the enum numbers, because the simulator parses them with int.Parse.
        /// </summary>
        public static string QuickDriveArguments(string pathFilePath, string consistFilePath, string startTime, SeasonType season, WeatherType weather) =>
            $"-start -explorer \"{pathFilePath}\" \"{consistFilePath}\" {startTime} {(int)season} {(int)weather}";

        /// <summary>Starts the simulator and completes once RunActivity.exe has exited.</summary>
        public static async Task RunAsync(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = RunActivityPath,
                Arguments = arguments,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = ApplicationInfo.ProcessDirectory,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start " + RunActivityPath + ".");
            await process.WaitForExitAsync();
        }
    }
}
