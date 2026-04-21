using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public sealed class MotherboardMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;

        // ── CPU Fan Auto-Detection State ───────────────────────────────────
        private string? _cpuFanSensorName = null;
        private bool _cpuFanResolved = false;
        private readonly Dictionary<string, float> _fanReadings = new();

        private static readonly string[] CpuFanCandidates =
            { "CPU Fan", "Fan #1", "Fan #2", "Fan #3", "Fan #4" };

        // ── Fan RPM Properties ─────────────────────────────────────────────
        private string _fan1Rpm = "N/A";
        private string _fan2Rpm = "N/A";
        private string _fan3Control = "N/A";

        // ── Temperature Properties ─────────────────────────────────────────
        private string _temp1 = "N/A";
        private string _temp2 = "N/A";
        private string _temp4 = "N/A";
        private string _temp5 = "N/A";
        private string _temp6 = "N/A";

        // ── Voltage Properties ─────────────────────────────────────────────
        private string _cpuVcore = "N/A";

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Fan RPM Getters/Setters ────────────────────────────────────────
        public string Fan1Rpm
        {
            get => _fan1Rpm;
            private set { if (_fan1Rpm != value) { _fan1Rpm = value; OnPropertyChanged(); } }
        }

        public string Fan2Rpm
        {
            get => _fan2Rpm;
            private set { if (_fan2Rpm != value) { _fan2Rpm = value; OnPropertyChanged(); } }
        }

        public string Fan3Control
        {
            get => _fan3Control;
            private set { if (_fan3Control != value) { _fan3Control = value; OnPropertyChanged(); } }
        }

        // ── Temperature Getters/Setters ────────────────────────────────────
        public string Temp1
        {
            get => _temp1;
            private set { if (_temp1 != value) { _temp1 = value; OnPropertyChanged(); } }
        }

        public string Temp2
        {
            get => _temp2;
            private set { if (_temp2 != value) { _temp2 = value; OnPropertyChanged(); } }
        }

        public string Temp4
        {
            get => _temp4;
            private set { if (_temp4 != value) { _temp4 = value; OnPropertyChanged(); } }
        }

        public string Temp5
        {
            get => _temp5;
            private set { if (_temp5 != value) { _temp5 = value; OnPropertyChanged(); } }
        }

        public string Temp6
        {
            get => _temp6;
            private set { if (_temp6 != value) { _temp6 = value; OnPropertyChanged(); } }
        }

        // ── Voltage Getters/Setters ────────────────────────────────────────
        public string CpuVcore
        {
            get => _cpuVcore;
            private set { if (_cpuVcore != value) { _cpuVcore = value; OnPropertyChanged(); } }
        }

        // ── Constructor ───────────────────────────────────────────────────
        public MotherboardMonitoringService()
        {
            InitializeHardwareMonitoring();
        }

        // ── Public Methods ────────────────────────────────────────────────
        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorMotherboardAsync(_cancellationTokenSource.Token);
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

        // ── Hardware Init ─────────────────────────────────────────────────
        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsMotherboardEnabled = true,
                };
                _computer.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Motherboard] Init error: {ex.Message}");
            }
        }

        // ── CPU Fan Auto-Detection ────────────────────────────────────────
        private static string? ResolveCpuFanName(Dictionary<string, float> readings)
        {
            // Priority 1: first candidate name that has an active RPM
            foreach (var name in CpuFanCandidates)
                if (readings.TryGetValue(name, out var rpm) && rpm > 0)
                    return name;

            // Priority 2: any active fan as last-resort fallback
            return readings
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Key)
                .FirstOrDefault();
        }

        // ── Monitor Loop ──────────────────────────────────────────────────
        private async Task MonitorMotherboardAsync(CancellationToken cancellationToken)
        {
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

                    var mbHardware = _computer?.Hardware
                        .FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

                    if (mbHardware != null)
                    {
                        foreach (var subHw in mbHardware.SubHardware)
                        {
                            // ── Pass 1: collect all sensor readings ───────
                            foreach (var sensor in subHw.Sensors)
                            {
                                // ── Fan RPM Collection ────────────────────
                                if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                                {
                                    _fanReadings[sensor.Name] = sensor.Value.Value;
                                }

                                // ── Fan Control (%) ───────────────────────
                                if (sensor.SensorType == SensorType.Control && sensor.Value.HasValue)
                                {
                                    if (sensor.Name == "Fan #3")
                                        Fan3Control = $"{sensor.Value.Value:F0}%";
                                }

                                // ── Temperatures ──────────────────────────
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                                {
                                    switch (sensor.Name)
                                    {
                                        case "Temperature #1":
                                            Temp1 = $"{sensor.Value.Value:F0}°C";
                                            Debug.WriteLine($"[Motherboard] Temp1: {sensor.Value.Value:F0}°C");
                                            break;
                                        case "Temperature #2":
                                            Temp2 = $"{sensor.Value.Value:F0}°C";
                                            break;
                                        case "Temperature #4":
                                            Temp4 = $"{sensor.Value.Value:F0}°C";
                                            break;
                                        case "Temperature #5":
                                            Temp5 = $"{sensor.Value.Value:F0}°C";
                                            break;
                                        case "Temperature #6":
                                            Temp6 = $"{sensor.Value.Value:F0}°C";
                                            break;
                                    }
                                }

                                // ── Voltages ──────────────────────────────
                                if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue)
                                {
                                    if (sensor.Name == "Voltage #1")
                                        CpuVcore = $"{sensor.Value.Value:F3} V";
                                }
                            }

                            // ── Pass 2: resolve CPU fan from collected readings ──
                            if (_fanReadings.Count > 0)
                            {
                                // Re-detect if not yet resolved, or if the
                                // previously resolved fan has dropped to 0 RPM
                                bool resolvedFanStalled = _cpuFanSensorName != null
                                    && _fanReadings.TryGetValue(_cpuFanSensorName, out var currentRpm)
                                    && currentRpm == 0;

                                if (!_cpuFanResolved || resolvedFanStalled)
                                {
                                    _cpuFanSensorName = ResolveCpuFanName(_fanReadings);
                                    _cpuFanResolved = _cpuFanSensorName != null;
                                    Debug.WriteLine($"[Motherboard] CPU fan resolved to: {_cpuFanSensorName ?? "none"}");
                                }

                                // ── Apply CPU fan RPM → Fan1Rpm ───────────
                                Fan1Rpm = _cpuFanSensorName != null
                                    && _fanReadings.TryGetValue(_cpuFanSensorName, out var cpuRpm)
                                    ? $"{cpuRpm:F0} RPM"
                                    : "N/A";

                                // ── Fan2Rpm = first other active fan ──────
                                var otherFan = _fanReadings
                                    .Where(kv => kv.Key != _cpuFanSensorName && kv.Value > 0)
                                    .OrderBy(kv => kv.Key)
                                    .FirstOrDefault();

                                Fan2Rpm = otherFan.Key != null
                                    ? $"{otherFan.Value:F0} RPM"
                                    : "N/A";

                                _fanReadings.Clear();
                            }
                        }
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Motherboard] Monitor error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}