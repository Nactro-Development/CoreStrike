using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CoreStrike
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        public MainWindow()
        {

            this.InitializeComponent();
            InitializeComponent();


            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += MainWindow_Loaded;
            }


        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!IsPawnIOInstalled())
            {
                await CheckAndInstallPawnIO();
            }


        }



        private bool IsPawnIOInstalled()
        {
            // Update "PawnIO" to the exact DisplayName or Registry Key name used by the installer
            string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey))
            {
                if (key != null)
                {
                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey != null && subkey.GetValue("DisplayName")?.ToString().Contains("PawnIO", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // Also check 32-bit registry if the app is 64-bit and installer is 32-bit
            string registryKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey32))
            {
                if (key != null)
                {
                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                        {
                            if (subkey != null && subkey.GetValue("DisplayName")?.ToString().Contains("PawnIO", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private async Task CheckAndInstallPawnIO()
        {
            ContentDialog installDialog = new ContentDialog
            {
                Title = "Install PawnIO",
                Content = "PawnIO needs to be installed to continue. Do you want to install it now?",
                PrimaryButtonText = "Install",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            ContentDialogResult result = await installDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await RunSilentInstaller();
            }
        }

        private async Task RunSilentInstaller()
        {
            try
            {
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "PawnIO_setup.exe");

                if (File.Exists(exePath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "-install -silent",
                        UseShellExecute = false, // 🔥 important
                        CreateNoWindow = true
                    };

                    Process process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        
                        // Restart the application
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle error
            }
        }


        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string tag = item.Tag.ToString();

                switch (tag)
                {
                    case "DashBord":
                        contentFrame.Navigate(typeof(DashBord.DashBord));
                        break;

                    case "BenchMarks":
                        contentFrame.Navigate(typeof(BenchMarks.BenchMarks));
                        break;

                    case "Ranking":
                        contentFrame.Navigate(typeof(Ranking.Ranking));
                        break;
                }
            }
        }


    }
}
