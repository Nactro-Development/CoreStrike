using CoreStrike.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CoreStrike.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {

      

        public SettingsPage()
        {

            InitializeComponent();


            window_Loaded();

        }


        private void window_Loaded()
        {
            var settings = SettingsService.Settings;

            AutoUpdateCheckBox.IsChecked = settings.CheckForUpdates;
        }


        private void AutoUpdateTrue(object sender, RoutedEventArgs e)
        {
            SettingsService.Settings.CheckForUpdates = true;
            SettingsService.Save();
        }

        private void AutoUpdateFalse(object sender, RoutedEventArgs e)
        {

            SettingsService.Settings.CheckForUpdates = false;
            SettingsService.Save();
        }


    }




    public class SettingsModel
    {
      
        public bool RunAtStartup { get; set; } = false;

        public bool CheckForUpdates { get; set; } = true;

    }


}
