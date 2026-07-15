using CoreStrike.Services;
using CoreStrike.Update;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Win32;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CoreStrike
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {

        private const string UpdateUrl =
        "https://raw.githubusercontent.com/Nactro-Development/CoreStrike/refs/heads/Update/Update/update.json";



        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            this.UnhandledException += App_UnhandledException;

            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException +=
                TaskScheduler_UnobservedTaskException;




        }





        private void App_UnhandledException(object sender,
      Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            CrashReporter.Save(e.Exception);

            var crash = new CrashWindow(e.Exception.ToString());
            crash.Activate();
        }




        private void CurrentDomain_UnhandledException(
            object sender,
            System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                CrashReporter.Save(ex);
            }
        }



        private void TaskScheduler_UnobservedTaskException(
            object sender,
            UnobservedTaskExceptionEventArgs e)
        {
            CrashReporter.Save(e.Exception);
            e.SetObserved();
        }




        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {

            await CrashReporter.UploadPendingCrashReportsAsync();

            SettingsService.Load();
        
            bool updateAvailable = await CheckForUpdates();

            if (!updateAvailable)
            {

                _window = new MainWindow();
                _window.Activate();


            }
        }


        private static string GetCurrentVersion()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CoreStrike");

                string? version = key?.GetValue("DisplayVersion")?.ToString();

                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }
            catch
            {
            }

            return "0.0.0.0";
        }



        private async Task<bool> CheckForUpdates()
        {
            try
            {
                using HttpClient client = new HttpClient();

                string json = await client.GetStringAsync(UpdateUrl);

                var update = System.Text.Json.JsonSerializer.Deserialize<UpdateInfo>(json);

                if (update == null)
                    return false;

                Version current = new Version(GetCurrentVersion());

                Version latest = new Version(update.version);

                if (latest > current)
                {
                    bool autoUpdate = SettingsService.Settings.CheckForUpdates;

                    var updateWindow = new UpdateWindow(update, autoUpdate);
                    updateWindow.Activate();

                    return true;
                }
            }
            catch
            {
                // Ignore network errors
            }

            return false;
        }





    }
}
