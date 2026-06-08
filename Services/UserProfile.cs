using System;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text.Json;


namespace CoreStrike
{
    public class UserProfile
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string PcName { get; set; } = "";
        public string PcModel { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string Country { get; set; } = "";

        private static string ProfilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CoreStrike",
                "user.json");

        public static UserProfile Load()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    string json = File.ReadAllText(ProfilePath);
                    return JsonSerializer.Deserialize<UserProfile>(json)!;
                }
            }

            catch (Exception ex)
            {
                File.WriteAllText(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "CoreStrike_Error.txt"),
                    ex.ToString());
            }

            var profile = new UserProfile
            {
                UserId = Guid.NewGuid().ToString(),
                UserName = Environment.UserName,
                PcName = Environment.MachineName,
                PcModel = GetPcModel(),
                CreatedAt = DateTime.Now,
                Country = RegionInfo.CurrentRegion.TwoLetterISORegionName
            };

            profile.Save();

            return profile;
        }

        public void Save()
        {
            string folder = Path.GetDirectoryName(ProfilePath)!;

            Directory.CreateDirectory(folder);

            File.WriteAllText(
                ProfilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        private static string GetPcModel()
        {
            try
            {
                using var searcher =
                    new ManagementObjectSearcher(
                        "SELECT Manufacturer, Model FROM Win32_ComputerSystem");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                    string model = obj["Model"]?.ToString() ?? "";

                    return $"{manufacturer} {model}";
                }
            }
            catch
            {
            }

            return "Unknown PC";
        }
    }
}