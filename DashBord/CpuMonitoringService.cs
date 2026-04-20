using LibreHardwareMonitor.Hardware;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Microsoft.UI.Xaml.Controls;
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
        private Axis? _xAxis;
        private float _maxCpuTemperature = 0;

        // ── New Power & Voltage fields ─────────────────────────────────────
        private string _cpuPackagePower = "N/A";
        private string _cpuCoreSvi2Voltage = "N/A";
        private string _cpuSocSvi2Voltage = "N/A";
        private string _cpuBusSpeed = "N/A";

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Existing Properties ───────────────────────────────────────────
        public float MaxCpuTemperature
        {
            get => _maxCpuTemperature;
            private set { if (_maxCpuTemperature != value) { _maxCpuTemperature = value; OnPropertyChanged(); } }
        }

        public void ResetMaxTemperature() => MaxCpuTemperature = 0;

        public void SetXAxis(Axis xAxis) => _xAxis = xAxis;

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

        public ObservableCollection<ObservablePoint> CpuUsageData => _cpuUsageData;

        // ── New Properties ────────────────────────────────────────────────

        /// <summary>CPU Package Power — e.g. "38.8 W"</summary>
        public string CpuPackagePower
        {
            get => _cpuPackagePower;
            private set { if (_cpuPackagePower != value) { _cpuPackagePower = value; OnPropertyChanged(); } }
        }

        /// <summary>Core SVI2 TFN Voltage — e.g. "1.350 V"</summary>
        public string CpuCoreSvi2Voltage
        {
            get => _cpuCoreSvi2Voltage;
            private set { if (_cpuCoreSvi2Voltage != value) { _cpuCoreSvi2Voltage = value; OnPropertyChanged(); } }
        }

        /// <summary>SoC SVI2 TFN Voltage — e.g. "1.550 V"</summary>
        public string CpuSocSvi2Voltage
        {
            get => _cpuSocSvi2Voltage;
            private set { if (_cpuSocSvi2Voltage != value) { _cpuSocSvi2Voltage = value; OnPropertyChanged(); } }
        }

        /// <summary>CPU Bus Speed — e.g. "100.0 MHz"</summary>
        public string CpuBusSpeed
        {
            get => _cpuBusSpeed;
            private set { if (_cpuBusSpeed != value) { _cpuBusSpeed = value; OnPropertyChanged(); } }
        }

        // ── Constructor ───────────────────────────────────────────────────
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

        public void StopMonitoring() => _cancellationTokenSource?.Cancel();

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
                    if (_computer != null)
                    {
                        foreach (var hardware in _computer.Hardware)
                        {
                            hardware.Update();
                            foreach (var subHardware in hardware.SubHardware)
                                subHardware.Update();
                        }
                    }

                    var cpuHardware = _computer?.Hardware
                        .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

                    if (cpuHardware != null)
                    {
                        CpuName = cpuHardware.Name;

                        float cpuUsage = 0;
                        float temperature = 0;
                        bool hasPackageTemp = false;
                        var coreClocks = new List<float>();

                        if (firstRun)
                        {
                            Debug.WriteLine($"=== CPU Hardware: {cpuHardware.Name} ===");
                            Debug.WriteLine($"Total Sensors: {cpuHardware.Sensors.Length}");
                            foreach (var s in cpuHardware.Sensors)
                                Debug.WriteLine($"  [{s.SensorType}] {s.Name} = {s.Value}");

                            var mbHw = _computer?.Hardware
                                .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
                            if (mbHw != null)
                            {
                                Debug.WriteLine("=== Motherboard SubHardware ===");
                                foreach (var sub in mbHw.SubHardware)
                                {
                                    Debug.WriteLine($"  SubHW: {sub.Name}");
                                    foreach (var s in sub.Sensors)
                                        Debug.WriteLine($"    [{s.SensorType}] {s.Name} = {s.Value}");
                                }
                            }
                            firstRun = false;
                        }

                        foreach (var sensor in cpuHardware.Sensors)
                        {
                            // ── CPU Load ───────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                if (sensor.Name.Contains("Total") || // Intel
                                    sensor.Name.Contains("Package") || // AMD might label total load as "CPU Package Load" or similar
                                    cpuUsage == 0)
                                {
                                    cpuUsage = sensor.Value ?? 0;
                                }
                            }

                            // ── CPU Clock ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                            {
                                string name = sensor.Name.ToLower();
                               
                                if (name.Contains("core")) // Only consider core clocks, ignore bus speed or other clocks
                                    coreClocks.Add(sensor.Value.Value);
                            }



                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                // ★★★ DEBUG OUTPUT ★★★
                                Console.WriteLine($"[DEBUG] Sensor: {sensor.Name} | Value: {sensor.Value} | Type: {sensor.SensorType}");

                                bool isPackage =
                                    sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tccd", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("CCD", StringComparison.OrdinalIgnoreCase);

                                Console.WriteLine($"[DEBUG] isPackage = {isPackage}");

                                if (isPackage)
                                {
                                    temperature = sensor.Value ?? 0;
                                    hasPackageTemp = true;
                                    Console.WriteLine($"[DEBUG] Package Temp Set: {temperature}");
                                }
                                else if (!hasPackageTemp)
                                {
                                    temperature = Math.Max(temperature, sensor.Value ?? 0);
                                    Console.WriteLine($"[DEBUG] Fallback Temp Set: {temperature}");
                                }
                            }


                            // ── Package Power ──────────────────────────────────
                            if (sensor.SensorType == SensorType.Power &&
                                (sensor.Name.Equals("Package", StringComparison.OrdinalIgnoreCase) || // Some AMD sensors might just be "CPU Power" or similar
                                 sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase)) && // Intel
                                sensor.Value.HasValue)
                            {
                                CpuPackagePower = $"{sensor.Value.Value:F1} W";
                            }



                            // ── Core voltage ──────────────────────────
                            if (sensor.SensorType == SensorType.Voltage &&
                                (sensor.Name.Equals("CPU Core", StringComparison.OrdinalIgnoreCase) || // Intel
                                 sensor.Name.Equals("Core (SVI2 TFN)", StringComparison.OrdinalIgnoreCase)) && // AMD
                                sensor.Value.HasValue)
                            {
                                CpuCoreSvi2Voltage = $"{sensor.Value.Value:F3} V";
                            }



                            // ── Bus speed ───────────────────────
                            if (sensor.SensorType == SensorType.Clock &&
                                (sensor.Name.Equals("Bus Speed", StringComparison.OrdinalIgnoreCase)) &&
                                sensor.Value.HasValue)
                            {
                                CpuBusSpeed = $"{sensor.Value.Value:F0} MHz";
                            }


                        }

                        // Fallback temperature from motherboard
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
                                            bool isCpuTemp =
                                                sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                                                sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                                sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                                                sensor.Name.Contains("Tccd", StringComparison.OrdinalIgnoreCase);

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

                        // Clock
                        float finalCpuClock = coreClocks.Count > 0 ? coreClocks.Average() : 0;
                        CpuSpeed = finalCpuClock > 0 ? $"{finalCpuClock / 1000:F2} GHz" : "N/A";
                        CpuCoresAvg = CpuSpeed;

                        Debug.WriteLine($"CpuSpeed: {finalCpuClock} MHz | Power: {CpuPackagePower} | CoreV: {CpuCoreSvi2Voltage} | SoCv: {CpuSocSvi2Voltage}");

                        // Temperature
                        CpuTemperature = hasPackageTemp && temperature >= 0
                            ? $"{temperature:F0}°C"
                            : "N/A";

                        if (temperature > MaxCpuTemperature)
                            MaxCpuTemperature = temperature;

                        CpuDisplayText = $"CPU Usage: {cpuUsage:F0}%";

                        // Chart data
                        _cpuUsageData.Add(new ObservablePoint(dataPointCount, cpuUsage));
                        if (_cpuUsageData.Count > 50)
                            _cpuUsageData.RemoveAt(0);

                        if (_xAxis != null)
                        {
                            if (dataPointCount < 50)
                            {
                                _xAxis.MinLimit = 0;
                                _xAxis.MaxLimit = 49;
                            }
                            else
                            {
                                _xAxis.MinLimit = dataPointCount - 49;
                                _xAxis.MaxLimit = dataPointCount;
                            }
                        }

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