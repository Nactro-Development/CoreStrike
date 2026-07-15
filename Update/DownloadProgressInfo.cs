using System;

namespace CoreStrike.Models
{
    public class DownloadProgressInfo
    {
        public double Percentage { get; set; }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }

        public double SpeedMBps { get; set; }

        public TimeSpan Remaining { get; set; }

        public double DownloadedMB =>
            DownloadedBytes / 1024d / 1024d;

        public double TotalMB =>
            TotalBytes / 1024d / 1024d;

        public string RemainingText =>
      Remaining.TotalSeconds <= 0
          ? "--:--"
          : Remaining.ToString(@"mm\:ss");


        public string ProgressText =>
            $"{DownloadedMB:F1} MB / {TotalMB:F1} MB";

        public string SpeedText =>
            $"{SpeedMBps:F1} MB/s";
    }
}