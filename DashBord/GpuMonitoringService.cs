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

        public void SetXAxis(Axis xAxis)
        {
            _xAxis = xAxis;
        }


        public void Set3DCopyXAsis(Axis x3DCopyAxis)
        {
            _x3DCopyAxis = x3DCopyAxis;
        }


        public void Set3DVEXAsis(Axis x3DVEAxis)
        {
            _x3DVEAxis = x3DVEAxis;
        }

        public void Set3DVDXAsis(Axis x3DVDAxis)
        {
            _x3DVDAxis = x3DVDAxis;
        }



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
        // ✅ CORRECT
        public string Gpu3DVDDisplayText
        {
            get => _gpu3DVDDisplayText;  // <-- fix
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

        public ObservableCollection<ObservablePoint> GpuUsageData
        {
            get => _gpuUsageData;
        }

        public ObservableCollection<ObservablePoint> Gpu3DCopyUsageData
        {
            get => _gpu3DCopyUsageData;
        }


        public ObservableCollection<ObservablePoint> Gpu3DVEUsageData
        {
            get => _gpu3DVEUsageData;
        }

        public ObservableCollection<ObservablePoint> Gpu3DVEDUsageData
        {
            get => _gpu3DVDUsageData;
        }

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

        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsGpuEnabled = true,
                    IsCpuEnabled = true, // CPU integrated graphics sometimes here
                };

                _computer.Open();

                // ✅ DEBUG: ALL hardware print කරන්න
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
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }


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

                    var gpuHardware = _gpuHardwareList.Count > 0 && _selectedGpuIndex < _gpuHardwareList.Count
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

                        // Debug on first run
                        if (firstRun)
                        {
                            Debug.WriteLine($"=== GPU Hardware: {gpuHardware.Name} ===");
                            Debug.WriteLine($"Total Sensors: {gpuHardware.Sensors.Length}");
                            foreach (var s in gpuHardware.Sensors)
                            {
                                Debug.WriteLine($"  [{s.SensorType}] {s.Name} = {s.Value}");
                            }
                            firstRun = false;
                        }

                        foreach (var sensor in gpuHardware.Sensors)
                        {
                            // ── GPU Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "d3d 3d")
                                {
                                    gpuUsage = sensor.Value ?? 0;
                                }
                             
                                // PRIORITY 3: Any GPU load as last resort
                                else if (gpuUsage == 0 && name.Contains("gpu"))
                                {
                                    gpuUsage = sensor.Value ?? 0;
                                }
                            }

                            // ── GPU Copy Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "d3d copy")
                                {
                                    gpuCopyUsage = sensor.Value ?? 0;
                                }
                             
                            
                            }

                            // ── GPU Fan ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Fan)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "gpu fan")
                                {
                                    gpufan = sensor.Value ?? 0;
                                }
                             
                            
                            }




                            // ── GPU Video Encode Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "d3d video encoder")
                                {
                                    gpuVEUsage = sensor.Value ?? 0;
                                }
                             
                            
                            }

                            // ── GPU Video Decode Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "d3d video decode")
                                {
                                    gpuVDUsage = sensor.Value ?? 0;
                                }
                             
                            
                            }
                            // ── GPU Power ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "gpu power")
                                {
                                    gpuPowerUsage = sensor.Value ?? 0;
                                }
                             
                            
                            }


                            // ── GPU Usage ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Load)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: D3D 3D usage (most accurate for gaming/rendering)
                                if (name == "gpu core")
                                {
                                    gpucoreUsage = sensor.Value ?? 0;
                                }
                             
                            
                            }


                            // ── GPU Clock ──────────────────────────────────────
                            if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: GPU Core clock (main shader clock)
                                if (name == "gpu core")
                                {
                                    gpuClock = sensor.Value.Value;
                                }
                                // PRIORITY 2: Any GPU clock as fallback
                                else if (gpuClock == 0 && name.Contains("gpu"))
                                {
                                    gpuClock = sensor.Value.Value;
                                }
                                // Skip memory clock
                            }



                            // ── GPU Temperature ────────────────────────────────
                            // ── GPU Temperature ────────────────────────────────
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                string name = sensor.Name.ToLower();

                                // PRIORITY 1: GPU Core temp (main die temp)
                                if (name == "gpu core")
                                {
                                    temperature = sensor.Value ?? 0;
                                }
                                // PRIORITY 2: Any other GPU temp as fallback (Hot Spot etc.)
                                else if (temperature == 0 && name.Contains("gpu"))
                                {
                                    temperature = sensor.Value ?? 0;
                                }
                            }


                            // ── GPU Memory ────────────────────────────────────
                            if (sensor.SensorType == SensorType.SmallData)
                            {
                                string name = sensor.Name.ToLower();

                                if (name.Contains("used"))
                                {
                                    memoryUsed = sensor.Value ?? 0;
                                }
                                else if (name.Contains("total"))
                                {
                                    memoryTotal = sensor.Value ?? 0;
                                }
                            }
                        }

                        GpuClock = gpuClock > 0
                            ? $"{gpuClock:F0} MHz"
                            : "N/A";

                        GpuTemperature = temperature >= 0
                            ? $"{temperature:F0}°C"
                            : "N/A";

                        // Format memory display
                        if (memoryUsed > 0 && memoryTotal > 0)
                        {
                            float usedGb = memoryUsed / 1024;
                            float totalGb = memoryTotal / 1024;
                            GpuMemoryUsed = $"{usedGb:F1} GB";
                            GpuMemoryTotal = $"{totalGb:F1} GB";
                        }
                        else
                        {
                            GpuMemoryUsed = "N/A";
                            GpuMemoryTotal = "N/A";
                        }

                        // Track maximum temperature
                        if (temperature > MaxGpuTemperature)
                        {
                            MaxGpuTemperature = temperature;
                        }

                        GpuDisplayText = $"3D: {gpuUsage:F0}%";
                        Gpu3DCopyDisplayText = $"Copy: {gpuCopyUsage:F0}%";
                        Gpu3DVEDisplayText = $"Video Encode: {gpuVEUsage:F0}%";
                        Gpu3DVDDisplayText = $"Video Decode: {gpuVDUsage:F0}%";
                        GPUfanText = $"{gpufan:F0} RPM";
                        GPUPowerText = $"{gpuPowerUsage:F0} W";
                        GPUCoreUsageText = $"GPU Usage: {gpucoreUsage:F0}%";

                        Debug.WriteLine($"Error monitoring GPU: {gpuCopyUsage}");
                        Debug.WriteLine($"Error monitoring GPU: {gpuVEUsage}");
                        Debug.WriteLine($"Error monitoring GPU: {gpufan}");




                        // Update chart data (keep last 50 points)
                        _gpuUsageData.Add(new ObservablePoint(dataPointCount, gpuUsage));
                        if (_gpuUsageData.Count > 50)
                            _gpuUsageData.RemoveAt(0);

                        // Sliding window X axis update
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



                        // Update chart data (keep last 50 points)
                        _gpu3DCopyUsageData.Add(new ObservablePoint(data3DCopyPointCount, gpuCopyUsage));
                        if (_gpu3DCopyUsageData.Count > 50)
                            _gpu3DCopyUsageData.RemoveAt(0);

                        // Sliding window X axis update
                        if (_x3DCopyAxis != null)
                        {
                            if (data3DCopyPointCount < 50)
                            {
                                _x3DCopyAxis.MinLimit = 0;
                                _x3DCopyAxis.MaxLimit = 49;
                            }
                            else
                            {
                                _x3DCopyAxis.MinLimit = data3DCopyPointCount - 49;
                                _x3DCopyAxis.MaxLimit = data3DCopyPointCount;
                            }
                        }

                        data3DCopyPointCount++;




                        // Update chart data (keep last 50 points)
                        _gpu3DVEUsageData.Add(new ObservablePoint(data3DVEPointCount, gpuVEUsage));
                        if (_gpu3DVEUsageData.Count > 50)
                            _gpu3DVEUsageData.RemoveAt(0);

                        // Sliding window X axis update
                        if (_x3DVEAxis != null)
                        {
                            if (data3DVEPointCount < 50)
                            {
                                _x3DVEAxis.MinLimit = 0;
                                _x3DVEAxis.MaxLimit = 49;
                            }
                            else
                            {
                                _x3DVEAxis.MinLimit = data3DVEPointCount - 49;
                                _x3DVEAxis.MaxLimit = data3DVEPointCount;
                            }
                        }

                        data3DVEPointCount++;




                        // Update chart data (keep last 50 points)
                        _gpu3DVDUsageData.Add(new ObservablePoint(data3DVDPointCount, gpuVDUsage));
                        if (_gpu3DVDUsageData.Count > 50)
                            _gpu3DVDUsageData.RemoveAt(0);

                        // Sliding window X axis update
                        if (_x3DVDAxis != null)
                        {
                            if (data3DVDPointCount < 50)
                            {
                                _x3DVDAxis.MinLimit = 0;
                                _x3DVDAxis.MaxLimit = 49;
                            }
                            else
                            {
                                _x3DVDAxis.MinLimit = data3DVDPointCount - 49;
                                _x3DVDAxis.MaxLimit = data3DVDPointCount;
                            }
                        }

                        data3DVDPointCount++;
                    }



                    else
                    {
                        GpuDisplayText = "No GPU detected";
                    }

                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring GPU: {ex.Message}");
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
