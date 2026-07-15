using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;

namespace CoreStrike.Services
{
    public static class InstallerService
    {
        public static void Install(string installerPath)
        {
            if (string.IsNullOrWhiteSpace(installerPath))
                throw new ArgumentException(nameof(installerPath));

            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Installer not found.", installerPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/silent",   // <-- Custom installer එකට /silent යවයි
                UseShellExecute = true,
                Verb = "runas"
            });

            Application.Current.Exit();
        }
    }
}