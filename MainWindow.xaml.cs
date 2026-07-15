using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using System;
using System.Net.Http;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Management;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using Microsoft.UI.Xaml.Media.Imaging;
using CoreStrike.Update;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CoreStrike
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {


        private const string CurrentVersion = "1.2.0.5";

        private const string UpdateUrl =
        "https://raw.githubusercontent.com/Nactro-Development/CoreStrike/refs/heads/Update/Update/update.json";



        private bool _initialized = false;
        public MainWindow()
        {
            
            InitializeComponent();
          

            var profile = UserProfile.Load();

            if (!string.IsNullOrEmpty(profile.UserName))
            {
                UserPicture.Initials =
                    profile.UserName.Substring(0, 1).ToUpper();
            }

            UserNameText.Text = $"User: {profile.UserName}";
            PcNameText.Text = $"PC: {profile.PcName}";
            UserName.Text = $"{profile.UserName}";
            PcName.Text = $"{profile.PcName}";
            PcModelText.Text = $"Model: {profile.PcModel}";
            UserIdText.Text = $"ID: {profile.UserId}";

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId id = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(id);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);

                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }


            appWindow.SetIcon("Assets/logo2.ico");



            CenterWindow();


            this.Activated += MainWindow_Activated;

            DashBord.DashBord.DashboardReady += () =>

            {

                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingGrid.Visibility = Visibility.Collapsed;
                    MainContentGrid.Visibility = Visibility.Visible;
                    ProfilePanel.Visibility = Visibility.Visible;
                });
            };
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {

          

            if (_initialized) return;
            _initialized = true;

            LoadingGrid.Visibility = Visibility.Visible;
            MainContentGrid.Visibility = Visibility.Collapsed;
            ProfilePanel.Visibility = Visibility.Collapsed;


            await Task.Delay(3000); // Simulate a delay

            contentFrame.Navigate(typeof(DashBord.DashBord));


            if (!IsPawnIOInstalled())
            {
                await CheckAndInstallPawnIO();
            }
        }




        private void CenterWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            var displayArea = DisplayArea.GetFromWindowId(
                windowId,
                DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                int centerX = displayArea.WorkArea.X +
                    (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;

                int centerY = displayArea.WorkArea.Y +
                    (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
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

            // Built-in Settings item
            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(Settings.SettingsPage));
                return;
            }


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

                    case "Donate":
                        contentFrame.Navigate(typeof(Donate.Donate));
                        break;

                    case "About":
                        contentFrame.Navigate(typeof(About.About));
                        break;
                }
            }
        }


       




    }

  


}
