using LibreHardwareMonitor.Hardware;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public sealed class MemoryMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;

        // ── Backing fields ────────────────────────────────────
        private string _memoryUsed        = "0 GB";
        private string _memoryAvailable   = "0 GB";
        private string _memoryTotal       = "0 GB";
        private string _memoryUsageText   = "0%";
        private string _memoryLoadText    = "Usage: 0%";
        private string _virtualUsed       = "0 GB";
        private string _virtualTotal      = "0 GB";
        private string _virtualUsage = "0 %";
        private float  _memoryUsagePercent = 0f;
        private float  _maxMemoryUsage    = 0f;

        private Axis? _xAxis;
        private Axis? _VxAxis;
        private int   _dataPointCount     = 0;
        private int   _VdataPointCount     = 0;


        private readonly ObservableCollection<ObservablePoint> _memoryUsageData = new();
        private readonly ObservableCollection<ObservablePoint> _VmemoryUsageData = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Public Properties ────────────────────────────────
        public string MemoryUsed
        {
            get => _memoryUsed;
            private set { if (_memoryUsed != value) { _memoryUsed = value; OnPropertyChanged(); } }
        }

        public string MemoryAvailable
        {
            get => _memoryAvailable;
            private set { if (_memoryAvailable != value) { _memoryAvailable = value; OnPropertyChanged(); } }
        }

        public string MemoryTotal
        {
            get => _memoryTotal;
            private set { if (_memoryTotal != value) { _memoryTotal = value; OnPropertyChanged(); } }
        }

        public string MemoryUsageText
        {
            get => _memoryUsageText;
            private set { if (_memoryUsageText != value) { _memoryUsageText = value; OnPropertyChanged(); } }
        }

        public string MemoryLoadText
        {
            get => _memoryLoadText;
            private set { if (_memoryLoadText != value) { _memoryLoadText = value; OnPropertyChanged(); } }
        }

        public string VirtualUsed
        {
            get => _virtualUsed;
            private set { if (_virtualUsed != value) { _virtualUsed = value; OnPropertyChanged(); } }
        }

        public string VirtualTotal
        {
            get => _virtualTotal;
            private set { if (_virtualTotal != value) { _virtualTotal = value; OnPropertyChanged(); } }
        }

        public string VirtualUsage
        {
            get => _virtualUsage;
            private set { if (_virtualUsage != value) { _virtualUsage = value; OnPropertyChanged(); } }
        }

        public float MemoryUsagePercent
        {
            get => _memoryUsagePercent;
            private set { if (_memoryUsagePercent != value) { _memoryUsagePercent = value; OnPropertyChanged(); } }
        }

        public float MaxMemoryUsage
        {
            get => _maxMemoryUsage;
            private set { if (_maxMemoryUsage != value) { _maxMemoryUsage = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<ObservablePoint> MemoryUsageData => _memoryUsageData;
        public ObservableCollection<ObservablePoint> VMemoryUsageData => _VmemoryUsageData;

        // ── Constructor ───────────────────────────────────────
        public MemoryMonitoringService()
        {
            InitializeHardwareMonitoring();
        }

        // ── Axis wiring (same pattern as GPU service) ─────────
        public void SetXAxis(Axis xAxis)
        {
            _xAxis = xAxis;
        }
        public void SetVXAxis(Axis VxAxis)
        {
            _VxAxis = VxAxis;
        }

        // ── Lifecycle ─────────────────────────────────────────
        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorMemoryAsync(_cancellationTokenSource.Token);
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

        public void ResetMaxUsage()
        {
            MaxMemoryUsage = 0;
        }

        // ── Hardware Init ─────────────────────────────────────
        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsMemoryEnabled = true,
                };
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing Memory hardware monitoring: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MemoryLoadText = $"Error: {ex.Message}";
            }
        }

        // ── Monitor Loop ──────────────────────────────────────
        private async Task MonitorMemoryAsync(CancellationToken cancellationToken)
        {
            bool firstRun = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_computer != null)
                    {
                        foreach (var hardware in _computer.Hardware)
                        {
                            hardware.Update();
                            foreach (var sub in hardware.SubHardware)
                                sub.Update();
                        }
                    }

                    float memoryLoad      = 0;
                    float memoryUsedMb    = 0;
                    float memoryAvailMb   = 0;
                    float virtualUsedMb   = 0;
                    float virtualTotalMb  = 0;
                    float vmemoryLoad     = 0;

                    if (_computer != null)
                    {
                        foreach (var hardware in _computer.Hardware)
                        {
                            if (hardware.HardwareType != HardwareType.Memory)
                                continue;

                            // Debug dump on first run
                            if (firstRun)
                            {
                                Debug.WriteLine($"=== Memory Hardware: {hardware.Name} ===");
                                Debug.WriteLine($"Total Sensors: {hardware.Sensors.Length}");
                                foreach (var s in hardware.Sensors)
                                    Debug.WriteLine($"  [{s.SensorType}] {s.Name} = {s.Value}");
                                firstRun = false;
                            }

                            foreach (var sensor in hardware.Sensors)
                            {
                                string name = sensor.Name.ToLower();
                                float  val  = sensor.Value ?? 0;

                                // ── Load ─────────────────────────────────
                                if (sensor.SensorType == SensorType.Load && name == "memory")
                                {
                                    memoryLoad = val;
                                }
                                // ── Load ─────────────────────────────────
                                if (sensor.SensorType == SensorType.Load && name == "virtual memory")
                                {
                                    vmemoryLoad = val;
                                }

                                // ── Physical Memory ───────────────────────
                                if (sensor.SensorType == SensorType.Data)
                                {
                                    if (name == "memory used")
                                        memoryUsedMb = val;
                                    else if (name == "memory available")
                                        memoryAvailMb = val;
                                }

                                // ── Virtual Memory ────────────────────────
                                if (sensor.SensorType == SensorType.Data)
                                {
                                    if (name == "virtual memory used")
                                        virtualUsedMb = val;
                                    else if (name == "virtual memory available")
                                        virtualTotalMb = val + virtualUsedMb; // total = used + free
                                }
                            }
                        }
                    }

                    // ── Derived values ────────────────────────────────
                    float totalMb = memoryUsedMb + memoryAvailMb;

                    MemoryUsagePercent = memoryLoad;
                    MemoryUsageText    = $"RAM Usage: {memoryLoad:F0}%";
                    MemoryLoadText     = $"Usage: {memoryLoad:F0}%";

                    MemoryUsed      = memoryUsedMb > 0  ? $"RAM Used: {memoryUsedMb:F1} GB"  : "N/A";
                    MemoryAvailable = memoryAvailMb > 0 ? $"{memoryAvailMb:F1} GB" : "N/A";
                    MemoryTotal     = totalMb > 0        ? $"{totalMb:F1} GB"       : "N/A";

                    VirtualUsed  = virtualUsedMb > 0  ? $"VRAM Used: {virtualUsedMb:F1} GB"  : "N/A";
                    VirtualTotal = virtualTotalMb > 0 ? $"{virtualTotalMb:F1} GB" : "N/A";
                    VirtualUsage = vmemoryLoad > 0 ? $"VRAM Usage: {vmemoryLoad:F1} %" : "N/A";

                    // Track peak
                    if (memoryLoad > MaxMemoryUsage)
                        MaxMemoryUsage = memoryLoad;



                    // ── Chart data (sliding 50-point window) ──────────
                    _memoryUsageData.Add(new ObservablePoint(_dataPointCount, memoryLoad));
                    if (_memoryUsageData.Count > 50)
                        _memoryUsageData.RemoveAt(0);

                    if (_xAxis != null)
                    {
                        if (_dataPointCount < 50)
                        {
                            _xAxis.MinLimit = 0;
                            _xAxis.MaxLimit = 49;
                        }
                        else
                        {
                            _xAxis.MinLimit = _dataPointCount - 49;
                            _xAxis.MaxLimit = _dataPointCount;
                        }
                    }

                    _dataPointCount++;


                    // ── Chart data (sliding 50-point window) ──────────
                    _VmemoryUsageData.Add(new ObservablePoint(_VdataPointCount, vmemoryLoad));
                    if (_VmemoryUsageData.Count > 50)
                        _VmemoryUsageData.RemoveAt(0);

                    if (_VxAxis != null)
                    {
                        if (_VdataPointCount < 50)
                        {
                            _VxAxis.MinLimit = 0;
                            _VxAxis.MaxLimit = 49;
                        }
                        else
                        {
                            _VxAxis.MinLimit = _VdataPointCount - 49;
                            _VxAxis.MaxLimit = _VdataPointCount;
                        }
                    }

                    _VdataPointCount++;

                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring Memory: {ex.Message}");
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
