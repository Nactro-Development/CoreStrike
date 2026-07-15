using CoreStrike.Models;
using CoreStrike.Services;
using CoreStrike.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;


namespace CoreStrike.Update
{
    public sealed partial class UpdateWindow : Window
    {
        private readonly UpdateInfo _update;
        private string? _installerPath;
      

        public UpdateWindow(UpdateInfo update, bool autoDownload = false)
        {
            InitializeComponent();

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32(1000, 600));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

            CenterWindow();

            _update = update;

            VersionText.Text = $"v{update.version}";
            updatetext.Text = update.releaseNotes;

            InstallButton.Visibility = Visibility.Collapsed;
            DownloadProgress.Visibility = Visibility.Collapsed;

            if (autoDownload)
            {
                this.Activated += UpdateWindow_Activated;
            }

        }

        private bool _started = false;


        private async void UpdateWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_started)
                return;

            _started = true;

            await StartDownloadAsync();
        }



        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            DownloadButton.IsEnabled = false;

            DownloadProgress.Visibility = Visibility.Visible;

            try
            {
                var progress = new Progress<DownloadProgressInfo>(info =>
                {
                    DownloadProgress.Value = info.Percentage;

                    // Progress percentage
                    PercentageText.Text = $"{info.Percentage:F0}%";

                    // Downloaded / Total
                    ProgressText.Text = info.ProgressText;

                    // Download speed
                    SpeedText.Text = info.SpeedText;

                    // Remaining time
                    RemainingText.Text = $"Remaining: {info.RemainingText}";
                });

                _installerPath = await DownloadService.DownloadAsync(
                    _update.downloadUrl,
                    progress);

                DownloadProgress.Value = 100;
                PercentageText.Text = "100%";

                if (SettingsService.Settings.CheckForUpdates)
                {
                    InstallerService.Install(_installerPath!);
                }
                else
                {
                    InstallButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                DownloadButton.IsEnabled = true;

                ContentDialog dialog = new()
                {
                    Title = "Download Failed",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }





        private void Install_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_installerPath))
                return;

            //InstallerService.Install(_installerPath);
        }

        private void DownloadLater_Click(object sender, RoutedEventArgs e)
        {
            new MainWindow().Activate();
            Close();
        }

        private void CenterWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            var displayArea = DisplayArea.GetFromWindowId(
                windowId,
                DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                appWindow.Move(new PointInt32(
                    displayArea.WorkArea.X + (displayArea.WorkArea.Width - appWindow.Size.Width) / 2,
                    displayArea.WorkArea.Y + (displayArea.WorkArea.Height - appWindow.Size.Height) / 2));
            }
        }
    }

    public class UpdateInfo
    {
        public string version { get; set; } = "";
        public string downloadUrl { get; set; } = "";
        public string releaseNotes { get; set; } = "";
    }
}