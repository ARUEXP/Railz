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

using System.Windows;
using Orts.Launcher.ViewModels;
using ORTS.Common;

namespace Orts.Launcher
{
    public partial class MainWindow : Window
    {
        readonly MainViewModel _viewModel = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();

            Title = $"Open Rails \u2014 Modern Launcher  {VersionInfo.VersionOrBuild}";
            DataContext = _viewModel;

            // Discovery touches disk and parses content, so it starts after the window is shown.
            Loaded += async (_, __) => await _viewModel.LoadFoldersAsync();
        }
    }
}
