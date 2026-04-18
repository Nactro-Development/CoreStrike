using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using LiveChartsCore.Defaults;
using Microsoft.UI.Xaml.Controls;

namespace CoreStrike.DashBord
{
    public sealed class CpuMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;
        private string _cpuName = string.Empty;
        private string _cpuSpeed = "0 MHz";
        private string _cpuTemperature = "0°C";
        private string _cpuDisplayText = string.Empty;
        private string _cpuCoresAvg = "N/A";
        private ObservableCollection<ObservablePoint> _cpuUsageData = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string CpuName
        {
            get => _cpuName;
            set { if (_cpuName != value) { _cpuName = value; OnPropertyChanged(); } }
        }

        public string CpuSpeed
        {
            get => _cpuSpeed;
            set { if (_cpuSpeed != value) { _cpuSpeed = value; OnPropertyChanged(); } }
        }

        public string CpuTemperature
        {
            get => _cpuTemperature;
            set { if (_cpuTemperature != value) { _cpuTemperature = value; OnPropertyChanged(); } }
        }

        public string CpuDisplayText
        {
            get => _cpuDisplayText;
            set { if (_cpuDisplayText != value) { _cpuDisplayText = value; OnPropertyChanged(); } }
        }

        public string CpuCoresAvg
        {
            get => _cpuCoresAvg;
            set { if (_cpuCoresAvg != value) { _cpuCoresAvg = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ObservablePoint> CpuUsageData
        {
            get => _cpuUsageData;
        }

        public CpuMonitoringService()
        {
            InitializeHardwareMonitoring();
        }

        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorCpuAsync(_cancellationTokenSource.Token);
            }
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _computer?.Close();
        }

        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsMotherboardEnabled = true,
                };

                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing hardware monitoring: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                CpuDisplayText = $"Error: {ex.Message}";
            }
        }

        private async Task MonitorCpuAsync(CancellationToken cancellationToken)
        {
            int dataPointCount = 0;
            bool firstRun = true;
            PerformanceCounter? cpuCounter = null;

            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                Debug.WriteLine("Performance Counter initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not initialize performance counter: {ex.Message}");
                cpuCounter = null;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Update all hardware and subhardware
                    if (_computer != null)
                    {
                        foreach (var hardware in _computer.Hardware)
                        {
                            hardware.Update();
                            foreach (var subHardware in hardware.SubHardware)
                            {
                                subHardware.Update();
                            }
                        }
                    }

                    var cpuHardware = _computer?.Hardware
                        .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

                    if (cpuHardware != null)
                    {
                        CpuName = cpuHardware.Name;

                        float cpuUsage = 0;
                        float cpuClock = 0;
                        float cpuClockPackage = 0;
                        float temperature = 0;
                        bool hasPackageTemp = false;
                        var coreClocks = new List<float>();

                        // Debug on first run - check Output window in Visual Studio
                        if (firstRun)
                        {
                            Debug.WriteLine($"=== CPU Hardware: {cpuHardware.Name} ===");
                            Debug.WriteLine($"Total Sensors: {cpuHardware.Sensors.Length}");
                            foreach (var s in cpuHardware.Sensors)
                            {
                                Debug.WriteLine($"  [{s.SensorType}] {s.Name} = {s.Value}");
                            }

                            // Also check motherboard subhardware for temps
                            var mbHardware = _computer?.Hardware
                                .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
                            if (mbHardware != null)
                            {
                                Debug.WriteLine($"=== Motherboard SubHardware ===");
                                foreach (var sub in mbHardware.SubHardware)
                                {
                                    Debug.WriteLine($"  SubHW: {sub.Name}");
                                    foreach (var s in sub.Sensors)
                                    {
                                        Debug.WriteLine($"    [{s.SensorType}] {s.Name} = {s.Value}");
                                    }
                                }
                            }

                            firstRun = false;
                        }

                        foreach (var sensor in cpuHardware.Sensors)
                        {
                            // ── CPU Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                if (sensor.Name.Contains("Total") ||
                                    sensor.Name.Contains("Package") ||
                                    cpuUsage == 0)
                                {
                                    cpuUsage = sensor.Value ?? 0;
                                }
                            }

                            if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                            {
                                string name = sensor.Name.ToLower();

                                // 🔴 PRIORITY 1: Best source
                                if (name.Contains("cores (average)"))
                                {
                                    cpuClock = sensor.Value.Value;
                                    break;
                                }
                            }

                            // ── CPU Temperature ────────────────────────────────
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                bool isPackage = sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                                             || sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                                             || sensor.Name.Contains("Tccd", StringComparison.OrdinalIgnoreCase)
                                             || sensor.Name.Contains("CCD", StringComparison.OrdinalIgnoreCase);

                                if (isPackage)
                                {
                                    temperature = sensor.Value ?? 0;
                                    hasPackageTemp = true;
                                }
                                else if (!hasPackageTemp)
                                {
                                    // Fallback: take highest core temp
                                    temperature = Math.Max(temperature, sensor.Value ?? 0);
                                }
                            }
                        }

                        // If no temperature found in CPU sensors, check motherboard subhardware
                        if (temperature == 0 && !hasPackageTemp)
                        {
                            var mbHardware = _computer?.Hardware
                                .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
                            if (mbHardware != null)
                            {
                                foreach (var subHw in mbHardware.SubHardware)
                                {
                                    foreach (var sensor in subHw.Sensors)
                                    {
                                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                                        {
                                            // Look for CPU-related temps
                                            bool isCpuTemp = sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                                                          || sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                                                          || sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                                                          || sensor.Name.Contains("Tccd", StringComparison.OrdinalIgnoreCase);

                                            if (isCpuTemp)
                                            {
                                                temperature = Math.Max(temperature, sensor.Value.Value);
                                                hasPackageTemp = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Prefer package clock if available, otherwise use highest core clock
                        float finalCpuClock = cpuClockPackage > 0 ? cpuClockPackage : cpuClock;

                        // Fallback for CPU usage via PerformanceCounter
                        if (cpuUsage == 0 && cpuCounter != null)
                        {
                            try { cpuUsage = cpuCounter.NextValue(); }
                            catch { /* ignore */ }
                        }

                        CpuSpeed = finalCpuClock > 0
                            ? $"{finalCpuClock / 1000:F2} GHz"
                            : "N/A";

                        // Calculate and display average core clock
                        if (coreClocks.Count > 0)
                        {
                            float avgCoreClock = coreClocks.Average();
                            CpuCoresAvg = $"{avgCoreClock / 1000:F2} GHz";
                        }
                        else
                        {
                            CpuCoresAvg = "N/A";
                        }

                        Debug.WriteLine($"CpuSpeed: {finalCpuClock} MHz");

                        CpuTemperature = hasPackageTemp && temperature >= 0
                            ? $"{temperature:F0}°C"
                            : "N/A";

                        CpuDisplayText = $"CPU Usage: {cpuUsage:F0}%";

                        // Update chart data (keep last 50 points)
                        _cpuUsageData.Add(new ObservablePoint(dataPointCount, cpuUsage));
                        if (_cpuUsageData.Count > 50)
                            _cpuUsageData.RemoveAt(0);

                        dataPointCount++;
                    }

                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring CPU: {ex.Message}");
                    await Task.Delay(500, cancellationToken);
                }
            }

            cpuCounter?.Dispose();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
