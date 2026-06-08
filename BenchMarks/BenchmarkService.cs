using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.Services
{
    public class FullBenchmarkResult
    {
        public int CpuScore { get; set; }
        public int MemoryScore { get; set; }
        public int DiskScore { get; set; }
        public int OverallScore { get; set; }
        public string Grade { get; set; } = "-";

        // Display info only
        public double CpuSeconds { get; set; }
        public double MemoryMBps { get; set; }
        public double DiskMBps { get; set; }
        public string CpuName { get; set; }
        public double CpuGHz { get; set; }
        public int CpuCores { get; set; }
        public int RamGB { get; set; }
        public string DiskType { get; set; }
    }


    public class SystemHardwareReport
    {
        public string CpuName { get; set; }
        public double CpuGHz { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public string CpuArchitecture { get; set; }

        public int RamGB { get; set; }
        public string RamType { get; set; }        // DDR4 / DDR5
        public int RamSpeedMHz { get; set; }
        public int RamSlots { get; set; }

        public string DiskType { get; set; }       // NVMe / SSD / HDD
        public string DiskModel { get; set; }
        public long DiskSizeGB { get; set; }

        public string GpuName { get; set; }
        public long GpuVramMB { get; set; }

        public string OsName { get; set; }
        public string OsVersion { get; set; }

        public string MotherboardModel { get; set; }
    }



    public class BenchmarkService
    {
        public async Task<FullBenchmarkResult> RunFullBenchmarkAsync()
        {
            return await Task.Run(() =>
            {
                var result = new FullBenchmarkResult();

                // --- Get hardware specs ---
                GetCpuInfo(out string cpuName, out double cpuGHz, out int cpuCores);
                result.CpuName = cpuName;
                result.CpuGHz = cpuGHz;
                result.CpuCores = cpuCores;
                result.RamGB = GetRamGB();
                result.DiskType = GetDiskType();

                // --- Scores purely from hardware specs (deterministic) ---
                result.CpuScore = ComputeCpuScore(cpuGHz, cpuCores);
                result.MemoryScore = ComputeRamScore(result.RamGB);
                result.DiskScore = ComputeDiskScore(result.DiskType);

                // --- Run perf tests for display info only (not used in score) ---
                result.CpuScore = result.CpuScore; // already set
                RunCpuBenchmark(out double cpuSec);
                result.CpuSeconds = cpuSec;

                RunMemoryBenchmark(out double memMbps);
                result.MemoryMBps = memMbps;

                RunDiskBenchmark(out double diskMbps);
                result.DiskMBps = diskMbps;

                // --- Overall score ---
                result.OverallScore =
                    (int)(result.CpuScore * 0.50 +
                          result.MemoryScore * 0.25 +
                          result.DiskScore * 0.25);

                result.Grade = GetGrade(result.OverallScore);

                return result;
            });
        }

        // ----------------------------------------------------------------
        // Hardware score functions - DETERMINISTIC
        // ----------------------------------------------------------------
        private int ComputeCpuScore(double ghz, int cores)
        {
            // i9-13900K 5.8GHz 24c => ~55,000 (S)
            // i7-12700  4.9GHz 12c => ~23,205 (A)
            // i5-12400  4.4GHz  6c => ~10,428 (B)
            // i3-8100   3.6GHz  4c => ~5,688  (C)
            return (int)(ghz * cores * 395);
        }

        private int ComputeRamScore(int ramGB)
        {
            // 32GB => 32000, 16GB => 16000, 8GB => 8000
            return ramGB * 1000;
        }

        private int ComputeDiskScore(string diskType)
        {
            return diskType switch
            {
                "NVMe" => 40000,
                "SSD" => 20000,
                "HDD" => 5000,
                _ => 10000
            };
        }

        // ----------------------------------------------------------------
        // WMI - CPU info
        // ----------------------------------------------------------------
        private void GetCpuInfo(out string name, out double ghz, out int cores)
        {
            name = "Unknown";
            ghz = 2.0;
            cores = Environment.ProcessorCount;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, MaxClockSpeed, NumberOfCores FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    name = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                    ghz = Convert.ToDouble(obj["MaxClockSpeed"]) / 1000.0;
                    cores = Convert.ToInt32(obj["NumberOfCores"]);
                    break;
                }
            }
            catch { }
        }

        // ----------------------------------------------------------------
        // WMI - RAM total
        // ----------------------------------------------------------------
        private int GetRamGB()
        {
            long total = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Capacity FROM Win32_PhysicalMemory");

                foreach (ManagementObject obj in searcher.Get())
                    total += Convert.ToInt64(obj["Capacity"]);
            }
            catch { }

            return (int)(total / (1024L * 1024 * 1024));
        }

        // ----------------------------------------------------------------
        // WMI - Disk type (NVMe / SSD / HDD)
        // ----------------------------------------------------------------
        private string GetDiskType()
        {
            try
            {
                // Method 1: Check MediaType via MSFT_PhysicalDisk (most reliable)
                using var searcher = new ManagementObjectSearcher(
                    @"\\.\root\microsoft\windows\storage",
                    "SELECT MediaType FROM MSFT_PhysicalDisk");

                foreach (ManagementObject obj in searcher.Get())
                {
                    uint mediaType = Convert.ToUInt32(obj["MediaType"]);

                    // MediaType: 3 = HDD, 4 = SSD, 5 = SCM
                    if (mediaType == 4 || mediaType == 5)
                    {
                        // Check if NVMe via BusType
                        using var busSearcher = new ManagementObjectSearcher(
                            @"\\.\root\microsoft\windows\storage",
                            "SELECT BusType FROM MSFT_PhysicalDisk");

                        foreach (ManagementObject busObj in busSearcher.Get())
                        {
                            uint busType = Convert.ToUInt32(busObj["BusType"]);
                            // BusType 17 = NVMe
                            if (busType == 17) return "NVMe";
                        }

                        return "SSD";
                    }

                    if (mediaType == 3) return "HDD";
                }
            }
            catch { }

            // Method 2: Fallback - check drive model name for NVMe keyword
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Model FROM Win32_DiskDrive");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string model = obj["Model"]?.ToString()?.ToUpper() ?? "";

                    if (model.Contains("NVME") || model.Contains("NVM"))
                        return "NVMe";

                    if (model.Contains("SSD") || model.Contains("SOLID"))
                        return "SSD";
                }
            }
            catch { }

            return "Unknown";
        }

        // ----------------------------------------------------------------
        // Performance runs - display info only, NOT used in score
        // ----------------------------------------------------------------
        private void RunCpuBenchmark(out double seconds)
        {
            int threads = Environment.ProcessorCount;
            long ops = 0;
            var sw = Stopwatch.StartNew();

            Parallel.For(0, threads, _ =>
            {
                long local = 0;
                for (long i = 0; i < 50_000_000; i++)
                {
                    Math.Sqrt(i);
                    local++;
                }
                Interlocked.Add(ref ops, local);
            });

            sw.Stop();
            seconds = sw.Elapsed.TotalSeconds;
        }

        private void RunMemoryBenchmark(out double mbps)
        {
            const int size = 512 * 1024 * 1024;
            byte[] buffer = new byte[size];
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < buffer.Length; i += 4096)
                buffer[i] = (byte)(i % 255);

            sw.Stop();
            mbps = (size / 1024d / 1024d) / sw.Elapsed.TotalSeconds;
        }

        private void RunDiskBenchmark(out double mbps)
        {
            string file = Path.Combine(Path.GetTempPath(), "corestrike_benchmark.tmp");
            byte[] data = new byte[200 * 1024 * 1024];
            new Random().NextBytes(data);

            var sw = Stopwatch.StartNew();
            File.WriteAllBytes(file, data);
            File.ReadAllBytes(file);
            sw.Stop();

            try { File.Delete(file); } catch { }

            mbps = 400d / sw.Elapsed.TotalSeconds;
        }

        private string GetGrade(int score)
        {
            if (score >= 50000) return "S";
            if (score >= 30000) return "A";
            if (score >= 15000) return "B";
            if (score >= 8000) return "C";
            return "D";
        }


        public async Task<SystemHardwareReport> GetHardwareReportAsync()
        {
            return await Task.Run(() =>
            {
                var report = new SystemHardwareReport();

                // --- CPU ---
                try
                {
                    using var s = new ManagementObjectSearcher(
                        "SELECT Name, MaxClockSpeed, NumberOfCores, NumberOfLogicalProcessors, Architecture FROM Win32_Processor");

                    foreach (ManagementObject obj in s.Get())
                    {
                        report.CpuName = obj["Name"]?.ToString()?.Trim();
                        report.CpuGHz = Convert.ToDouble(obj["MaxClockSpeed"]) / 1000.0;
                        report.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                        report.CpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);

                        uint arch = Convert.ToUInt32(obj["Architecture"]);
                        report.CpuArchitecture = arch switch
                        {
                            9 => "x64",
                            5 => "ARM",
                            12 => "ARM64",
                            _ => "x86"
                        };
                        break;
                    }
                }
                catch { }

                // --- RAM ---
                try
                {
                    long total = 0;
                    int slots = 0;
                    int speed = 0;
                    string ramType = "Unknown";

                    using var s = new ManagementObjectSearcher(
                        "SELECT Capacity, Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory");

                    foreach (ManagementObject obj in s.Get())
                    {
                        total += Convert.ToInt64(obj["Capacity"]);
                        speed = Convert.ToInt32(obj["Speed"]);
                        slots++;

                        uint memType = Convert.ToUInt32(obj["SMBIOSMemoryType"]);
                        ramType = memType switch
                        {
                            26 => "DDR4",
                            34 => "DDR5",
                            24 => "DDR3",
                            _ => "Unknown"
                        };
                    }

                    report.RamGB = (int)(total / (1024L * 1024 * 1024));
                    report.RamSpeedMHz = speed;
                    report.RamSlots = slots;
                    report.RamType = ramType;
                }
                catch { }

                // --- Disk ---
                try
                {
                    using var s = new ManagementObjectSearcher(
                        "SELECT Model, Size FROM Win32_DiskDrive");

                    foreach (ManagementObject obj in s.Get())
                    {
                        report.DiskModel = obj["Model"]?.ToString()?.Trim();
                        report.DiskSizeGB = Convert.ToInt64(obj["Size"]) / (1024L * 1024 * 1024);
                        break;
                    }

                    report.DiskType = GetDiskType(); // reuse existing method
                }
                catch { }

                // --- GPU ---
                try
                {
                    using var s = new ManagementObjectSearcher(
                        "SELECT Name, AdapterRAM FROM Win32_VideoController");

                    foreach (ManagementObject obj in s.Get())
                    {
                        string gpuName = obj["Name"]?.ToString() ?? "";

                        // Skip Microsoft Basic / Remote Display adapters
                        if (gpuName.Contains("Microsoft Basic") ||
                            gpuName.Contains("Remote"))
                            continue;

                        report.GpuName = gpuName.Trim();
                        report.GpuVramMB = Convert.ToInt64(obj["AdapterRAM"]) / (1024L * 1024);
                        break;
                    }
                }
                catch { }

                // --- OS ---
                try
                {
                    using var s = new ManagementObjectSearcher(
                        "SELECT Caption, Version FROM Win32_OperatingSystem");

                    foreach (ManagementObject obj in s.Get())
                    {
                        report.OsName = obj["Caption"]?.ToString()?.Trim();
                        report.OsVersion = obj["Version"]?.ToString();
                        break;
                    }
                }
                catch { }

                // --- Motherboard ---
                try
                {
                    using var s = new ManagementObjectSearcher(
                        "SELECT Product FROM Win32_BaseBoard");

                    foreach (ManagementObject obj in s.Get())
                    {
                        report.MotherboardModel = obj["Product"]?.ToString()?.Trim();
                        break;
                    }
                }
                catch { }

                return report;
            });
        }


    }
}