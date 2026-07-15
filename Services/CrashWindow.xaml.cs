using CoreStrike;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using WinRT.Interop;
namespace CoreStrike;

public sealed partial class CrashWindow : Window
{
    public CrashWindow(string error)
    {
        InitializeComponent();

        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 600));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;      // Resize disable
            presenter.IsMaximizable = false;    // Maximize button hide
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;     // (Optional) Always on top
        }


        ErrorText.Text = error;
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        SendReportButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        await CrashReporter.UploadPendingCrashReportsAsync();

        Environment.Exit(0);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }
}