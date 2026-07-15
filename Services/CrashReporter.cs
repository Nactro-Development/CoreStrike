using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreStrike
{
    public static class CrashReporter
    {
        private static readonly string CrashFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CoreStrike",   
                "CrashReports");

        // ⚠️ Replace with your real Discord Webhook
        private const string DiscordWebhook =
            "https://discord.com/api/webhooks/1526933854018076816/hntJF1Wd87WZqXRhkUD1FJkDtyQNqAwGh1XEkSqGDBq0inH3kWf0-R-RO6AljExzvEDX";

        public static void Save(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(CrashFolder);

                var info = new CrashInfo
                {
                    AppVersion = Assembly.GetExecutingAssembly()
          .GetName()
          .Version?
          .ToString() ?? "Unknown",

                    WindowsVersion = RuntimeInformation.OSDescription,
                    DotNetVersion = RuntimeInformation.FrameworkDescription,
                    Architecture = RuntimeInformation.OSArchitecture.ToString(),

                    Time = DateTime.Now,

                    ExceptionType = ex.GetType().FullName ?? "",
                    Message = ex.Message,
                    StackTrace = ex.ToString(),
                    InnerException = ex.InnerException?.ToString() ?? "",

                    // NEW
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    CurrentCulture = System.Globalization.CultureInfo.CurrentCulture.Name,
                    Memory = GC.GetTotalMemory(false),
                    ProcessId = Environment.ProcessId,


                    AppPath = Environment.ProcessPath ?? "",
                    CommandLine = Environment.CommandLine,
                    ProcessorCount = Environment.ProcessorCount,
                    Is64BitOS = Environment.Is64BitOperatingSystem,
                    Is64BitProcess = Environment.Is64BitProcess


                };



                string file = Path.Combine(
                    CrashFolder,
                    $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                File.WriteAllText(
                    file,
                    JsonSerializer.Serialize(
                        info,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
            }
            catch
            {
                // Never throw from crash reporter
            }
        }

        public static async Task UploadPendingCrashReportsAsync()
        {
            try
            {
                if (!Directory.Exists(CrashFolder))
                    return;

                string[] files = Directory.GetFiles(CrashFolder, "*.json");

                using HttpClient client = new HttpClient();

                foreach (string file in files)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file);

                        var payload = new
                        {
                            content =
$@"🚨 **CoreStrike Crash Report**

```json
{json}
```"
                        };

                        string body = JsonSerializer.Serialize(payload);

                        using var response = await client.PostAsync(
                            DiscordWebhook,
                            new StringContent(
                                body,
                                Encoding.UTF8,
                                "application/json"));

                        if (response.IsSuccessStatusCode)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Keep the file for the next launch
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}