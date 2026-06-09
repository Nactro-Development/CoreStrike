using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace CoreStrikeSetup
{
    public sealed partial class MainWindow : Window
    {
        private readonly string InstallPath =
            @"C:\Program Files\CoreStrike";

        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(Path.Combine(InstallPath, "CoreStrike.exe")))
            {
                StatusText.Text = "CoreStrike already installed";
                LaunchButton.Visibility = Visibility.Visible;
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InstallButton.IsEnabled = false;

                StatusText.Text = "Creating folders...";
                InstallProgress.Value = 10;

                Directory.CreateDirectory(InstallPath);

                string zipPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "app.zip");

                if (!File.Exists(zipPath))
                {
                    StatusText.Text = "app.zip not found";
                    return;
                }

                StatusText.Text = "Extracting files...";
                InstallProgress.Value = 40;

                ZipFile.ExtractToDirectory(
                    zipPath,
                    InstallPath,
                    true);

                InstallProgress.Value = 75;

                StatusText.Text = "Creating shortcuts...";

                CreateDesktopShortcut();
                CreateStartMenuShortcut();

                InstallProgress.Value = 100;

                StatusText.Text = "Installation Complete";

                LaunchButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally
            {
                InstallButton.IsEnabled = true;
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            string exePath =
                Path.Combine(InstallPath, "CoreStrike.exe");

            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
        }

        private void CreateDesktopShortcut()
        {
            string desktop =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop);

            string shortcutLocation =
                Path.Combine(desktop, "CoreStrike.lnk");

            WshShell shell = new();

            IWshShortcut shortcut =
                (IWshShortcut)shell.CreateShortcut(shortcutLocation);

            shortcut.TargetPath =
                Path.Combine(InstallPath, "CoreStrike.exe");

            shortcut.WorkingDirectory =
                InstallPath;

            shortcut.Save();
        }

        private void CreateStartMenuShortcut()
        {
            string startMenu =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonPrograms);

            string folder =
                Path.Combine(startMenu, "CoreStrike");

            Directory.CreateDirectory(folder);

            string shortcutLocation =
                Path.Combine(folder, "CoreStrike.lnk");

            WshShell shell = new();

            IWshShortcut shortcut =
                (IWshShortcut)shell.CreateShortcut(shortcutLocation);

            shortcut.TargetPath =
                Path.Combine(InstallPath, "CoreStrike.exe");

            shortcut.WorkingDirectory =
                InstallPath;

            shortcut.Save();
        }
    }
}