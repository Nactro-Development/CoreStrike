using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public sealed class StorageMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;

        // ── Backing fields ─────────────────────────────────────
        private List<string> _availableDrives = new();
        private int _selectedDriveIndex = 0;

        private string _driveName = string.Empty;
        private string _driveModel = string.Empty;
        private string _driveHealth = "N/A";
        private string _driveTemperature = "N/A";
        private string _driveUsedSpace = "N/A";
        private string _driveFreeSpace = "N/A";
        private string _driveTotalSpace = "N/A";
        private string _driveUsageText = "N/A";
        private string _driveReadRate = "N/A";
        private string _driveWriteRate = "N/A";
        private string _driveThroughput = "N/A";
        private string _driveType = "N/A";   // SSD / HDD / NVMe
        private string _powerOnHours = "N/A";
        private string _driveLifeText = "N/A";   // remaining life %

        private string _systemTotalSpace = "N/A";
        private string _systemUsedSpace = "N/A";
        private string _systemFreeSpace = "N/A";

        // Maps display name → LibreHardwareMonitor IHardware
        private Dictionary<string, IHardware> _driveMap = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Public Properties ──────────────────────────────────
        public List<string> AvailableDrives
        {
            get => _availableDrives;
            private set { _availableDrives = value; OnPropertyChanged(); }
        }

        public int SelectedDriveIndex
        {
            get => _selectedDriveIndex;
            set
            {
                if (_selectedDriveIndex != value)
                {
                    _selectedDriveIndex = value;
                    OnPropertyChanged();
                    RefreshSelectedDrive();
                }
            }
        }

        public string DriveName
        {
            get => _driveName;
            private set { if (_driveName != value) { _driveName = value; OnPropertyChanged(); } }
        }

        public string DriveModel
        {
            get => _driveModel;
            private set { if (_driveModel != value) { _driveModel = value; OnPropertyChanged(); } }
        }

        public string DriveHealth
        {
            get => _driveHealth;
            private set { if (_driveHealth != value) { _driveHealth = value; OnPropertyChanged(); } }
        }

        public string DriveTemperature
        {
            get => _driveTemperature;
            private set { if (_driveTemperature != value) { _driveTemperature = value; OnPropertyChanged(); } }
        }

        public string DriveUsedSpace
        {
            get => _driveUsedSpace;
            private set { if (_driveUsedSpace != value) { _driveUsedSpace = value; OnPropertyChanged(); } }
        }

        public string DriveFreeSpace
        {
            get => _driveFreeSpace;
            private set { if (_driveFreeSpace != value) { _driveFreeSpace = value; OnPropertyChanged(); } }
        }

        public string DriveTotalSpace
        {
            get => _driveTotalSpace;
            private set { if (_driveTotalSpace != value) { _driveTotalSpace = value; OnPropertyChanged(); } }
        }

        public string DriveUsageText
        {
            get => _driveUsageText;
            private set { if (_driveUsageText != value) { _driveUsageText = value; OnPropertyChanged(); } }
        }

        public string DriveReadRate
        {
            get => _driveReadRate;
            private set { if (_driveReadRate != value) { _driveReadRate = value; OnPropertyChanged(); } }
        }

        public string DriveWriteRate
        {
            get => _driveWriteRate;
            private set { if (_driveWriteRate != value) { _driveWriteRate = value; OnPropertyChanged(); } }
        }

        public string DriveThroughput
        {
            get => _driveThroughput;
            private set { if (_driveThroughput != value) { _driveThroughput = value; OnPropertyChanged(); } }
        }

        public string DriveType
        {
            get => _driveType;
            private set { if (_driveType != value) { _driveType = value; OnPropertyChanged(); } }
        }

        public string PowerOnHours
        {
            get => _powerOnHours;
            private set { if (_powerOnHours != value) { _powerOnHours = value; OnPropertyChanged(); } }
        }

        public string DriveLifeText
        {
            get => _driveLifeText;
            private set { if (_driveLifeText != value) { _driveLifeText = value; OnPropertyChanged(); } }
        }

        public string SystemTotalSpace
        {
            get => _systemTotalSpace;
            private set { if (_systemTotalSpace != value) { _systemTotalSpace = value; OnPropertyChanged(); } }
        }

        public string SystemUsedSpace
        {
            get => _systemUsedSpace;
            private set { if (_systemUsedSpace != value) { _systemUsedSpace = value; OnPropertyChanged(); } }
        }

        public string SystemFreeSpace
        {
            get => _systemFreeSpace;
            private set { if (_systemFreeSpace != value) { _systemFreeSpace = value; OnPropertyChanged(); } }
        }

        // ── Constructor ────────────────────────────────────────
        public StorageMonitoringService()
        {
            InitializeHardwareMonitoring();
        }

        // ── Lifecycle ──────────────────────────────────────────
        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorStorageAsync(_cancellationTokenSource.Token);
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

        // ── Hardware Init ──────────────────────────────────────
        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsStorageEnabled = true,
                };
                _computer.Open();

                BuildDriveList();
                UpdateSystemTotalStorage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Storage init error: {ex.Message}");
            }
        }

        // ── Build combo box list ───────────────────────────────
        private void BuildDriveList()
        {
            _driveMap.Clear();
            var names = new List<string>();

            if (_computer == null) return;

            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Storage)
                    continue;

                // Display name: "Samsung SSD 980 (C:, D:)" style
                string label = BuildDriveLabel(hw);
                _driveMap[label] = hw;
                names.Add(label);
            }

            AvailableDrives = names;

            if (names.Count > 0)
                RefreshSelectedDrive();
        }

        // "Samsung 980 Pro (C:)" — append Windows drive letters if available
        private static string BuildDriveLabel(IHardware hw)
        {
            string model = hw.Name.Trim();

            // Try matching physical drive index from hw.Identifier
            // e.g. /hdd/0  → PhysicalDrive0
            string ident = hw.Identifier.ToString();
            string letters = string.Empty;

            if (int.TryParse(ident.Split('/').LastOrDefault(), out int idx))
            {
                letters = GetDriveLabelForPhysical(idx);
            }

            return string.IsNullOrEmpty(letters) ? model : $"{model}  ({letters})";
        }

        // Get drive letter display for physical drive
        private static string GetDriveLabelForPhysical(int physicalIndex)
        {
            try
            {
                var driveLetters = GetDriveLettersForPhysicalDriveStatic(physicalIndex);
                if (driveLetters.Count > 0)
                {
                    return string.Join(", ", driveLetters.Select(c => $"{c}:"));
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // ── Refresh selected drive immediately ────────────────
        private void RefreshSelectedDrive()
        {
            if (_availableDrives.Count == 0) return;
            int idx = Math.Clamp(_selectedDriveIndex, 0, _availableDrives.Count - 1);
            string key = _availableDrives[idx];
            if (!_driveMap.TryGetValue(key, out var hw)) return;

            hw.Update();
            ParseAndPublish(hw);
        }

        // ── Monitor Loop ───────────────────────────────────────
        private async Task MonitorStorageAsync(CancellationToken cancellationToken)
        {
            bool firstRun = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_computer != null)
                    {
                        // Rebuild list on first run so late-detected drives appear
                        if (firstRun)
                        {
                            BuildDriveList();
                            UpdateSystemTotalStorage();
                            firstRun = false;
                        }

                        // Update system storage every 5 seconds
                        if (DateTime.UtcNow.Second % 5 == 0)
                        {
                            UpdateSystemTotalStorage();
                        }

                        // Update only the selected drive
                        int idx = Math.Clamp(_selectedDriveIndex, 0, _availableDrives.Count - 1);
                        if (_availableDrives.Count > 0)
                        {
                            string key = _availableDrives[idx];
                            if (_driveMap.TryGetValue(key, out var hw))
                            {
                                hw.Update();
                                ParseAndPublish(hw);
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
                    Debug.WriteLine($"Storage monitor error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        // ── Parse sensors and update properties ───────────────
        private bool _dumped = false;
        private void ParseAndPublish(IHardware hw)
        {
            if (!_dumped)
            {
                Debug.WriteLine($"=== Storage Hardware: {hw.Name} ===");
                Debug.WriteLine($"Total Sensors: {hw.Sensors.Length}");
                foreach (var s in hw.Sensors)
                    Debug.WriteLine($"  [{s.SensorType}] '{s.Name}' = {s.Value}");
                _dumped = true;
            }

            string model = hw.Name.Trim();
            float temperature = 0;
            float usedBytes = 0;
            float totalBytes = 0;
            float readRate = 0;
            float writeRate = 0;
            float life = -1;   // -1 = not available
            float powerOn = -1;
            float usageLoad = 0;
            float usageWrite = 0;
            float spare = 0;
            float spareThreshold = 0;

            foreach (var sensor in hw.Sensors)
            {
                string name = sensor.Name.ToLowerInvariant();
                float val = sensor.Value ?? 0;

                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (name.Contains("temperature") || name == "drive temperature")
                            temperature = val;
                        break;

                    case SensorType.Load:
                        // Some drives expose "used space" as a load sensor
                        if (name.Contains("used space"))
                            usageLoad = val;
                        else if (name.Contains("total activity"))
                            usageWrite = val;
                        break;

                 

                    case SensorType.Data:
                        if (name == "data read" || name.Contains("total bytes read"))
                            readRate = val;          // GB total — we'll show it as-is
                        else if (name == "data written" || name.Contains("total bytes written"))
                            writeRate = val;
                        break;

                    case SensorType.Throughput:
                        if (name.Contains("read"))
                            readRate = val;          // MB/s
                        else if (name.Contains("write"))
                            writeRate = val;
                        break;

                    case SensorType.Level:
                        {
                            string n = sensor.Name.Trim();

                            if (n.Equals("Available Spare", StringComparison.OrdinalIgnoreCase))
                                spare = val;

                            else if (n.Equals("Available Spare Threshold", StringComparison.OrdinalIgnoreCase))
                                spareThreshold = val;

                            else if (n.Equals("Percentage Used", StringComparison.OrdinalIgnoreCase))
                                life = val;

                            break;
                        }


                    case SensorType.SmallData:
                        if (name.Contains("power-on") || name.Contains("power on hours"))
                            powerOn = val;
                        break;
                }
            }

            // ── Drive space via DriveInfo (Windows letters) ───
            GetDriveSpaceFromOS(hw, out usedBytes, out totalBytes);

            // ── Determine type from name keywords ─────────────
            string typeName = model.ToUpperInvariant() switch
            {
                var m when m.Contains("NVME") || m.Contains("NVM") => "NVMe SSD",
                var m when m.Contains("SSD") => "SSD",
                _ => "HDD",
            };

            // ── Publish ───────────────────────────────────────
            DriveName = model;
            DriveModel = model;
            DriveType = typeName;

            DriveTemperature = temperature > 0
                ? $"{temperature:F0}°C"
                : "N/A";

            if (totalBytes > 0)
            {
                float usedGb = usedBytes / 1e9f;
                float totalGb = totalBytes / 1e9f;
                float freeGb = (totalBytes - usedBytes) / 1e9f;
                float pct = usedBytes / totalBytes * 100f;
               

                DriveUsedSpace = $"Used: {usedGb:F1} GB";
                DriveFreeSpace = $"Free: {freeGb:F1} GB";
                DriveTotalSpace = $"{totalGb:F1} GB";
                DriveUsageText = $"Total Activity: {usageWrite:F0}%";
            }
            else
            {
                DriveUsedSpace = "N/A";
                DriveFreeSpace = "N/A";
                DriveTotalSpace = "N/A";
                DriveUsageText = "N/A";
            }

            // Read / Write — distinguish throughput (MB/s) vs total data (GB)
            bool isThroughput = hw.Sensors.Any(s => s.SensorType == SensorType.Throughput);
            if (isThroughput)
            {
                DriveReadRate = readRate > 0 ? $"Read:  {readRate:F1} MB/s" : "Read:  0 MB/s";
                DriveWriteRate = writeRate > 0 ? $"Write: {writeRate:F1} MB/s" : "Write: 0 MB/s";
                DriveThroughput = $"{readRate + writeRate:F1} MB/s";
            }
            else
            {
                // Total-data sensors (Data type) are in GB
                DriveReadRate = readRate > 0 ? $"Total Read:  {readRate:F1} GB" : "N/A";
                DriveWriteRate = writeRate > 0 ? $"Total Write: {writeRate:F1} GB" : "N/A";
                DriveThroughput = "N/A";
            }
            float helth = spare - life;
            Debug.WriteLine($"spare: {spare}");

            bool hasLifeData = life >= 0;
            bool hasSpareData = spare > 0;

            if (hasLifeData && hasSpareData)
            {
                float health = 100 - life;
                DriveLifeText = $"Life: {health:F0}%";
            }
            else
            {
                DriveLifeText = "N/A";
            }


            PowerOnHours = powerOn >= 0 ? $"Power-On: {powerOn:F0} hrs" : "N/A";

            // SMART health: if life available use it, otherwise "Good" / "Unknown"
            DriveHealth = life >= 0
                ? (life > 50 ? "Good" : life > 20 ? "Warning" : "Critical")
                : "N/A";
        }

        // ── OS disk space ─────────────────────────────────────
        private static void GetDriveSpaceFromOS(IHardware hw, out float usedBytes, out float totalBytes)
        {
            usedBytes = 0;
            totalBytes = 0;

            try
            {
                // Extract physical drive index from hw.Identifier
                // Format: /hdd/0 or /hdd/1 etc.
                string ident = hw.Identifier.ToString();
                int physicalIndex = -1;

                if (int.TryParse(ident.Split('/').LastOrDefault(), out int idx))
                {
                    physicalIndex = idx;
                }

                // Get drive letters and match by physical drive
                // This is a simplified approach without WMI
                var driveLetters = GetDriveLettersForPhysicalDriveStatic(physicalIndex);

                if (driveLetters.Count > 0)
                {
                    foreach (var letter in driveLetters)
                    {
                        try
                        {
                            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.StartsWith(letter.ToString()));
                            if (drive != null && drive.IsReady)
                            {
                                totalBytes += drive.TotalSize;
                                usedBytes += drive.TotalSize - drive.AvailableFreeSpace;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DriveInfo error: {ex.Message}");
            }
        }

        // ── Get drive letters for physical drive (simplified) ──
        private static List<char> GetDriveLettersForPhysicalDriveStatic(int physicalIndex)
        {
            var letters = new List<char>();

            try
            {
                // Fallback: Try to match drive by checking all drives
                // In a single-drive system or without WMI, return all ready drives
                // For multi-drive accurate mapping, WMI is needed.
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == System.IO.DriveType.Fixed)
                    {
                        letters.Add(drive.Name[0]);
                    }
                }

                // Limit to the approximate physical drive count
                // (This is a rough heuristic without WMI)
                if (letters.Count > physicalIndex + 1)
                {
                    return letters.GetRange(physicalIndex, 1);
                }

                return letters;
            }
            catch
            {
                return letters;
            }
        }

        // ── Calculate total system storage ──────────────────
        private void UpdateSystemTotalStorage()
        {
            float totalBytes = 0;
            float usedBytes = 0;

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    totalBytes += drive.TotalSize;
                    usedBytes += drive.TotalSize - drive.AvailableFreeSpace;
                }

                if (totalBytes > 0)
                {
                    float totalGb = totalBytes / 1e9f;
                    float usedGb = usedBytes / 1e9f;
                    float freeGb = (totalBytes - usedBytes) / 1e9f;

                    SystemTotalSpace = $"Total: {totalGb:F1} GB";
                    SystemUsedSpace = $"Used: {usedGb:F1} GB";
                    SystemFreeSpace = $"Free: {freeGb:F1} GB";
                }
                else
                {
                    SystemTotalSpace = "N/A";
                    SystemUsedSpace = "N/A";
                    SystemFreeSpace = "N/A";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"System storage error: {ex.Message}");
                SystemTotalSpace = "N/A";
                SystemUsedSpace = "N/A";
                SystemFreeSpace = "N/A";
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}