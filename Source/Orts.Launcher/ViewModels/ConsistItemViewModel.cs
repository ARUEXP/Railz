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

using System.IO;
using Orts.Formats.Msts;
using ORTS.Menu;

namespace Orts.Launcher.ViewModels
{
    /// <summary>
    /// Wraps an <see cref="ORTS.Menu.Consist"/> for the consist browser.
    /// <see cref="LoadDetails"/> performs the extra format reads and is meant to run on a background thread.
    /// </summary>
    public sealed class ConsistItemViewModel : ViewModelBase
    {
        public ConsistItemViewModel(Consist consist)
        {
            Consist = consist;
        }

        public Consist Consist { get; }

        public string Name => LoaderText.Friendly(Consist.Name, "Custom Consist", FilePath);

        public string LocomotiveName => Consist.Locomotive == null
            ? "Any Locomotive"
            : LoaderText.Friendly(Consist.Locomotive.Name, "Custom Locomotive", LocomotivePath);

        /// <summary>Full path of the lead locomotive's .eng file, or null.</summary>
        public string? LocomotivePath => Consist.Locomotive?.FilePath;

        public string FilePath => Consist.FilePath;

        string? _locomotiveType;
        public string? LocomotiveType
        {
            get => _locomotiveType;
            private set => SetProperty(ref _locomotiveType, value);
        }

        int? _carCount;
        public int? CarCount
        {
            get => _carCount;
            private set { if (SetProperty(ref _carCount, value)) OnPropertyChanged(nameof(HasCarCount)); }
        }

        /// <summary>False when the consist could not be read, so the car count badge stays hidden.</summary>
        public bool HasCarCount => _carCount.HasValue;

        /// <summary>
        /// Reads the wagon count and power type. Neither is exposed by ORTS.Menu, so the
        /// consist and engine files are read again through the public format readers.
        /// </summary>
        public void LoadDetails()
        {
            try
            {
                CarCount = new ConsistFile(FilePath, false).Train.TrainCfg.WagonList.Count;
            }
            catch
            {
                CarCount = null;
            }

            try
            {
                LocomotiveType = LocomotivePath != null && File.Exists(LocomotivePath)
                    ? new EngineFile(LocomotivePath).EngineType
                    : null;
            }
            catch
            {
                LocomotiveType = null;
            }
        }
    }
}
