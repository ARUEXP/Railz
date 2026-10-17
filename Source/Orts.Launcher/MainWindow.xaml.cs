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
using System.Windows;
using System.Windows.Input;
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

        void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select your game library folder (the one containing ROUTES and TRAINS)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                _ = _viewModel.AddFolderAsync(dialog.SelectedPath);
        }

        async void DriveNow_Click(object sender, RoutedEventArgs e)
        {
            var arguments = _viewModel.LaunchArguments;

            // Alt+click is the testing aid: copy the exact command instead of starting the simulator.
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                if (arguments == null)
                {
                    _viewModel.StatusMessage = "Nothing to launch yet \u2014 " + _viewModel.LaunchProblem;
                    return;
                }

                try
                {
                    Clipboard.SetText(arguments);
                    _viewModel.StatusMessage = "Launch command copied to the clipboard.";
                }
                catch (Exception ex)
                {
                    _viewModel.StatusMessage = "Could not copy the launch command: " + ex.Message;
                }

                return;
            }

            // Nothing to start: the inline tip above the button already explains why.
            if (arguments == null)
                return;

            WindowState = WindowState.Minimized;
            try
            {
                await _viewModel.LaunchAsync();
            }
            finally
            {
                WindowState = WindowState.Normal;
                Activate();
            }
        }
    }
}
