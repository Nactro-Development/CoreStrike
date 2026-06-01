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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public sealed class GpuMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;
        private List<IHardware> _gpuHardwareList = new();
        private int _selectedGpuIndex = 0;
        private string _gpuName = string.Empty;
        private string _gpuClock = "0 MHz";
        private string _gpuTemperature = "0°C";
        private string _gpuDisplayText = string.Empty;
        private string _gpu3DCopyDisplayText = string.Empty;
        private string _gpu3DVEDisplayText = string.Empty;
        private string _gpufan = string.Empty;
        private string _gpupower = string.Empty;
        private string _gpu3DVDDisplayText = string.Empty;
        private string _gpucoreusage = string.Empty;
        private string _gpuMemoryUsed = "0 MB";
        private string _gpuMemoryTotal = "0 MB";
        private ObservableCollection<ObservablePoint> _gpuUsageData = new();
        private ObservableCollection<ObservablePoint> _gpu3DCopyUsageData = new();
        private ObservableCollection<ObservablePoint> _gpu3DVEUsageData = new();
        private ObservableCollection<ObservablePoint> _gpu3DVDUsageData = new();

        private Axis? _xAxis;
        private Axis? _x3DCopyAxis;
        private Axis? _x3DVEAxis;
        private Axis? _x3DVDAxis;

        private float _maxGpuTemperature = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Small helper record for fan detection ──────────────────────────
        private record FanSensor(string Name, float Rpm);

        // ══════════════════════════════════════════════════════════════════
        // Properties
        // ══════════════════════════════════════════════════════════════════

        public float MaxGpuTemperature
        {
            get => _maxGpuTemperature;
            private set { if (_maxGpuTemperature != value) { _maxGpuTemperature = value; OnPropertyChanged(); } }
        }

        public List<string> AvailableGpus { get; private set; } = new();

        public int SelectedGpuIndex
        {
            get => _selectedGpuIndex;
            set
            {
                if (_selectedGpuIndex != value)
                {
                    _selectedGpuIndex = value;
                    OnPropertyChanged();
                    ResetGpuData();
                }
            }
        }

        public void ResetMaxTemperature()
        {
            MaxGpuTemperature = 0;
        }

        private void ResetGpuData()
        {
            GpuName = string.Empty;
            GpuClock = "0 MHz";
            GpuTemperature = "0°C";
            GpuDisplayText = string.Empty;
            Gpu3DCopyDisplayText = string.Empty;
            Gpu3DVEDisplayText = string.Empty;
            Gpu3DVDDisplayText = string.Empty;
            GPUfanText = string.Empty;
            GPUPowerText = string.Empty;
            GPUCoreUsageText = string.Empty;
            GpuMemoryUsed = "0 MB";
            GpuMemoryTotal = "0 MB";
            _gpuUsageData.Clear();
            _gpu3DCopyUsageData.Clear();
            _gpu3DVEUsageData.Clear();
            _gpu3DVDUsageData.Clear();
        }

        public void SetXAxis(Axis xAxis) => _xAxis = xAxis;
        public void Set3DCopyXAsis(Axis x3DCopyAxis) => _x3DCopyAxis = x3DCopyAxis;
        public void Set3DVEXAsis(Axis x3DVEAxis) => _x3DVEAxis = x3DVEAxis;
        public void Set3DVDXAsis(Axis x3DVDAxis) => _x3DVDAxis = x3DVDAxis;

        public string GpuName
        {
            get => _gpuName;
            set { if (_gpuName != value) { _gpuName = value; OnPropertyChanged(); } }
        }

        public string GpuClock
        {
            get => _gpuClock;
            set { if (_gpuClock != value) { _gpuClock = value; OnPropertyChanged(); } }
        }

        public string GpuTemperature
        {
            get => _gpuTemperature;
            set { if (_gpuTemperature != value) { _gpuTemperature = value; OnPropertyChanged(); } }
        }

        public string GPUfanText
        {
            get => _gpufan;
            set { if (_gpufan != value) { _gpufan = value; OnPropertyChanged(); } }
        }

        public string GPUPowerText
        {
            get => _gpupower;
            set { if (_gpupower != value) { _gpupower = value; OnPropertyChanged(); } }
        }

        public string GPUCoreUsageText
        {
            get => _gpucoreusage;
            set { if (_gpucoreusage != value) { _gpucoreusage = value; OnPropertyChanged(); } }
        }

        public string GpuDisplayText
        {
            get => _gpuDisplayText;
            set { if (_gpuDisplayText != value) { _gpuDisplayText = value; OnPropertyChanged(); } }
        }

        public string Gpu3DCopyDisplayText
        {
            get => _gpu3DCopyDisplayText;
            set { if (_gpu3DCopyDisplayText != value) { _gpu3DCopyDisplayText = value; OnPropertyChanged(); } }
        }

        public string Gpu3DVEDisplayText
        {
            get => _gpu3DVEDisplayText;
            set { if (_gpu3DVEDisplayText != value) { _gpu3DVEDisplayText = value; OnPropertyChanged(); } }
        }

        public string Gpu3DVDDisplayText
        {
            get => _gpu3DVDDisplayText;
            set { if (_gpu3DVDDisplayText != value) { _gpu3DVDDisplayText = value; OnPropertyChanged(); } }
        }

        public string GpuMemoryUsed
        {
            get => _gpuMemoryUsed;
            set { if (_gpuMemoryUsed != value) { _gpuMemoryUsed = value; OnPropertyChanged(); } }
        }

        public string GpuMemoryTotal
        {
            get => _gpuMemoryTotal;
            set { if (_gpuMemoryTotal != value) { _gpuMemoryTotal = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ObservablePoint> GpuUsageData => _gpuUsageData;
        public ObservableCollection<ObservablePoint> Gpu3DCopyUsageData => _gpu3DCopyUsageData;
        public ObservableCollection<ObservablePoint> Gpu3DVEUsageData => _gpu3DVEUsageData;
        public ObservableCollection<ObservablePoint> Gpu3DVEDUsageData => _gpu3DVDUsageData;

        // ══════════════════════════════════════════════════════════════════
        // Constructor & Lifecycle
        // ══════════════════════════════════════════════════════════════════

        public GpuMonitoringService()
        {
            InitializeHardwareMonitoring();
        }

        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorGpuAsync(_cancellationTokenSource.Token);
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

        // ══════════════════════════════════════════════════════════════════
        // Hardware Initialization
        // ══════════════════════════════════════════════════════════════════

        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsGpuEnabled = true,
                    IsCpuEnabled = true,
                };

                _computer.Open();

                foreach (var hardware in _computer.Hardware)
                {
                    Debug.WriteLine($"Found Hardware: [{hardware.HardwareType}] {hardware.Name}");
                }

                AvailableGpus.Clear();
                _gpuHardwareList.Clear();

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuNvidia ||
                        hardware.HardwareType == HardwareType.GpuIntel ||
                        hardware.HardwareType == HardwareType.GpuAmd)
                    {
                        _gpuHardwareList.Add(hardware);
                        AvailableGpus.Add(hardware.Name);
                        Debug.WriteLine($"✅ Added GPU: {hardware.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing hardware: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Main Monitoring Loop
        // ══════════════════════════════════════════════════════════════════

        private async Task MonitorGpuAsync(CancellationToken cancellationToken)
        {
            int dataPointCount = 0;
            int data3DCopyPointCount = 0;
            int data3DVEPointCount = 0;
            int data3DVDPointCount = 0;
            bool firstRun = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // ── Update hardware on thread pool (non-blocking) ──
                    await Task.Run(() =>
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
                    }, cancellationToken);

                    var gpuHardware = (_gpuHardwareList.Count > 0 && _selectedGpuIndex < _gpuHardwareList.Count)
                        ? _gpuHardwareList[_selectedGpuIndex]
                        : null;

                    if (gpuHardware != null)
                    {
                        GpuName = gpuHardware.Name;

                        float gpuUsage = 0;
                        float gpuCopyUsage = 0;
                        float gpuClock = 0;
                        float temperature = 0;
                        float memoryUsed = 0;
                        float memoryTotal = 0;
                        float gpuVEUsage = 0;
                        float gpuVDUsage = 0;
                        float gpufan = 0;
                        float gpuPowerUsage = 0;
                        float gpucoreUsage = 0;

                        // Debug: print all sensors on first run
                        if (firstRun)
                        {
                            Debug.WriteLine($"=== GPU Hardware: {gpuHardware.Name} ===");
                            Debug.WriteLine($"Total Sensors: {gpuHardware.Sensors.Length}");
                            foreach (var s in gpuHardware.Sensors)
                                Debug.WriteLine($"  [{s.SensorType}] {s.Name} = {s.Value}");
                            firstRun = false;
                        }

                        // ── Read GPU sensors ───────────────────────────────────────
                        foreach (var sensor in gpuHardware.Sensors)
                        {
                            string nameLower = sensor.Name.ToLower();

                            // Load sensors
                            if (sensor.SensorType == SensorType.Load)
                            {
                                if (nameLower == "d3d 3d")
                                    gpuUsage = sensor.Value ?? 0;
                                else if (gpuUsage == 0 && nameLower.Contains("gpu"))
                                    gpuUsage = sensor.Value ?? 0;

                                if (nameLower == "d3d copy")
                                    gpuCopyUsage = sensor.Value ?? 0;

                                if (nameLower == "d3d video encoder")
                                    gpuVEUsage = sensor.Value ?? 0;

                                if (nameLower == "d3d video decode")
                                    gpuVDUsage = sensor.Value ?? 0;

                                if (nameLower == "gpu power")
                                    gpuPowerUsage = sensor.Value ?? 0;

                                if (nameLower == "gpu core")
                                    gpucoreUsage = sensor.Value ?? 0;
                            }

                            // Fan sensor (discrete GPU only — iGPU overridden below)
                            if (sensor.SensorType == SensorType.Fan && nameLower == "gpu fan")
                                gpufan = sensor.Value ?? 0;

                            // Clock
                            if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                            {
                                if (nameLower == "gpu core")
                                    gpuClock = sensor.Value.Value;
                                else if (gpuClock == 0 && nameLower.Contains("gpu"))
                                    gpuClock = sensor.Value.Value;
                            }

                            // Temperature (discrete GPU only — iGPU overridden below)
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (nameLower == "gpu core")
                                    temperature = sensor.Value ?? 0;
                                else if (temperature == 0 && nameLower.Contains("gpu"))
                                    temperature = sensor.Value ?? 0;
                            }

                            // Memory
                            if (sensor.SensorType == SensorType.SmallData)
                            {
                                if (nameLower.Contains("used"))
                                    memoryUsed = sensor.Value ?? 0;
                                else if (nameLower.Contains("total"))
                                    memoryTotal = sensor.Value ?? 0;
                            }
                        }

                        // ── Integrated GPU: override fan & temperature from CPU ──────
                        if (IsIntegratedGpu(gpuHardware))
                        {
                            var cpuHardware = _computer?.Hardware
                                .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

                            if (cpuHardware != null)
                            {
                                cpuHardware.Update();

                                // Temperature: CPU Package > CPU Core #x > any CPU temp
                                float cpuTemp = 0;
                                foreach (var sensor in cpuHardware.Sensors)
                                {
                                    if (sensor.SensorType == SensorType.Temperature)
                                    {
                                        if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tccd", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("CCD", StringComparison.OrdinalIgnoreCase) && 
                                    
                                    cpuTemp == 0)
                                        {
                                            cpuTemp = sensor.Value ?? 0;
                                        }
                                    }
                                }

                                if (cpuTemp > 0)
                                {
                                    temperature = cpuTemp;
                                    Debug.WriteLine($"iGPU → CPU temp used: {cpuTemp}°C");
                                }

                                // Fan: smart auto-detection
                                // Priority 1 → sensor named exactly "CPU Fan"
                                // Priority 2 → first fan sensor with RPM > 0
                                // Priority 3 → first fan sensor (even if 0)
                                var fanResult = FindBestCpuFan(cpuHardware);
                                if (fanResult != null)
                                {
                                    gpufan = fanResult.Rpm;
                                    Debug.WriteLine($"iGPU → CPU fan used: [{fanResult.Name}] = {fanResult.Rpm} RPM");
                                }
                            }
                        }

                        // ── Format display strings ─────────────────────────────────
                        GpuClock = gpuClock > 0 ? $"{gpuClock:F0} MHz" : "N/A";
                        GpuTemperature = temperature >= 0 ? $"{temperature:F0}°C" : "N/A";

                        if (memoryUsed > 0 && memoryTotal > 0)
                        {
                            GpuMemoryUsed = $"{memoryUsed / 1024:F1} GB";
                            GpuMemoryTotal = $"{memoryTotal / 1024:F1} GB";
                        }
                        else
                        {
                            GpuMemoryUsed = "N/A";
                            GpuMemoryTotal = "N/A";
                        }

                        if (temperature > MaxGpuTemperature)
                            MaxGpuTemperature = temperature;

                        GpuDisplayText = $"3D: {gpuUsage:F0}%";
                        Gpu3DCopyDisplayText = $"Copy: {gpuCopyUsage:F0}%";
                        Gpu3DVEDisplayText = $"Video Encode: {gpuVEUsage:F0}%";
                        Gpu3DVDDisplayText = $"Video Decode: {gpuVDUsage:F0}%";
                        GPUfanText = $"{gpufan:F0} RPM";
                        GPUPowerText = $"{gpuPowerUsage:F0} W";
                        GPUCoreUsageText = $"GPU Usage: {gpucoreUsage:F0}%";

                        // ── Update chart data (sliding window, 50 points) ──────────

                        // 3D Usage
                        _gpuUsageData.Add(new ObservablePoint(dataPointCount, gpuUsage));
                        if (_gpuUsageData.Count > 50) _gpuUsageData.RemoveAt(0);
                        UpdateAxisWindow(_xAxis, dataPointCount);
                        dataPointCount++;

                        // Copy Usage
                        _gpu3DCopyUsageData.Add(new ObservablePoint(data3DCopyPointCount, gpuCopyUsage));
                        if (_gpu3DCopyUsageData.Count > 50) _gpu3DCopyUsageData.RemoveAt(0);
                        UpdateAxisWindow(_x3DCopyAxis, data3DCopyPointCount);
                        data3DCopyPointCount++;

                        // Video Encode Usage
                        _gpu3DVEUsageData.Add(new ObservablePoint(data3DVEPointCount, gpuVEUsage));
                        if (_gpu3DVEUsageData.Count > 50) _gpu3DVEUsageData.RemoveAt(0);
                        UpdateAxisWindow(_x3DVEAxis, data3DVEPointCount);
                        data3DVEPointCount++;

                        // Video Decode Usage
                        _gpu3DVDUsageData.Add(new ObservablePoint(data3DVDPointCount, gpuVDUsage));
                        if (_gpu3DVDUsageData.Count > 50) _gpu3DVDUsageData.RemoveAt(0);
                        UpdateAxisWindow(_x3DVDAxis, data3DVDPointCount);
                        data3DVDPointCount++;
                    }
                    else
                    {
                        GpuDisplayText = "No GPU detected";
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring GPU: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Slides the X axis window forward as new data points arrive.
        /// </summary>
        private static void UpdateAxisWindow(Axis? axis, int pointCount)
        {
            if (axis == null) return;
            if (pointCount < 50)
            {
                axis.MinLimit = 0;
                axis.MaxLimit = 49;
            }
            else
            {
                axis.MinLimit = pointCount - 49;
                axis.MaxLimit = pointCount;
            }
        }

        /// <summary>
        /// Returns true when the selected GPU is an integrated GPU (Intel iGPU or
        /// AMD integrated Radeon Graphics), meaning its fan and temperature must be
        /// read from the CPU hardware instead.
        /// </summary>
        private static bool IsIntegratedGpu(IHardware hardware)
        {
            // All Intel GPUs reported by LibreHardwareMonitor are integrated
            if (hardware.HardwareType == HardwareType.GpuIntel)
                return true;

            // AMD: discrete cards contain "rx ", "pro ", "fury", "vega 56/64", "vii"
            // AMD integrated cards are "Radeon Graphics", "Vega 3/7/8/11", etc.
            if (hardware.HardwareType == HardwareType.GpuAmd)
            {
                string name = hardware.Name.ToLower();
                bool likelyDiscrete = name.Contains("rx ") ||
                                      name.Contains(" pro ") ||
                                      name.Contains("fury") ||
                                      name.Contains("vega 56") ||
                                      name.Contains("vega 64") ||
                                      name.Contains(" vii");
                return !likelyDiscrete;
            }

            return false; // NVIDIA cards are always discrete
        }

        /// <summary>
        /// Finds the most appropriate CPU fan sensor using the following priority:
        ///   1. Sensor whose name is exactly "CPU Fan"  (Board A)
        ///   2. First fan sensor with RPM > 0           (Board B)
        ///   3. First fan sensor regardless of RPM      (Board C fallback)
        /// Returns null if no fan sensors exist at all.
        /// </summary>
        private static FanSensor? FindBestCpuFan(IHardware cpuHardware)
        {
            FanSensor? firstActive = null;
            FanSensor? firstAny = null;

            foreach (var sensor in cpuHardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Fan) continue;

                float rpm = sensor.Value ?? 0;
                string name = sensor.Name;

                // Priority 1: exact "CPU Fan" match
                if (name.ToLower() == "cpu fan")
                    return new FanSensor(name, rpm);

                // Priority 2: first active fan
                if (firstActive == null && rpm > 0)
                    firstActive = new FanSensor(name, rpm);

                // Priority 3: first any fan
                if (firstAny == null)
                    firstAny = new FanSensor(name, rpm);
            }

            return firstActive ?? firstAny;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}