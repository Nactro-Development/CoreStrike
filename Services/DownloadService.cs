using CoreStrike.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CoreStrike.Services
{
    public static class DownloadService
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> DownloadAsync(
            string url,
            IProgress<DownloadProgressInfo>? progress = null)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CoreStrike",
                "Updates");

            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, "CoreStrikeSetup.exe");

            using HttpResponseMessage response =
                await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);   // <-- UI thread eken potha ganne na

            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? 0;

            await using Stream downloadStream =
                await response.Content.ReadAsStreamAsync()
                    .ConfigureAwait(false);   // <--

            await using FileStream fileStream = new(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                true);

            byte[] buffer = new byte[81920]; // 1MB eka wada wediyi, 80KB tikak balance

            long totalRead = 0;
            int bytesRead;

            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch reportThrottle = Stopwatch.StartNew();
            const int reportIntervalMs = 150; // methanin thamai UI queue flood wenne nathi widiyata throttle karanne

            while ((bytesRead = await downloadStream
                        .ReadAsync(buffer.AsMemory(0, buffer.Length))
                        .ConfigureAwait(false)) > 0)   // <--
            {
                await fileStream
                    .WriteAsync(buffer.AsMemory(0, bytesRead))
                    .ConfigureAwait(false);   // <--

                totalRead += bytesRead;

                if (totalBytes > 0 &&
                    (reportThrottle.ElapsedMilliseconds >= reportIntervalMs || totalRead == totalBytes))
                {
                    reportThrottle.Restart();

                    double percent =
                        (double)totalRead / totalBytes * 100;

                    double speedMBps =
                        totalRead / 1024d / 1024d /
                        Math.Max(stopwatch.Elapsed.TotalSeconds, 0.1);

                    double remainingSeconds =
                        speedMBps > 0
                        ? ((totalBytes - totalRead) / 1024d / 1024d) / speedMBps
                        : 0;

                    progress?.Report(new DownloadProgressInfo
                    {
                        Percentage = percent,
                        DownloadedBytes = totalRead,
                        TotalBytes = totalBytes,
                        SpeedMBps = speedMBps,
                        Remaining = TimeSpan.FromSeconds(remainingSeconds)
                    });
                }
            }

            await fileStream.FlushAsync().ConfigureAwait(false); // <--

            return filePath;
        }
    }
}