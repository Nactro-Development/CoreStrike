using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public sealed class NetworkMonitoringService : INotifyPropertyChanged
    {
        private Computer? _computer;
        private CancellationTokenSource? _cancellationTokenSource;
        private List<NetworkInterface> _networkInterfaces = new();
        private NetworkInterface? _selectedInterface;
        private string _adapterName = "Not Connected";
        private string _downloadSpeed = "0 Mbps";
        private string _uploadSpeed = "0 Mbps";
        private string _latency = "0 ms";
        private long _lastBytesSent = 0;
        private long _lastBytesReceived = 0;
        private DateTime _lastCheck = DateTime.UtcNow;
        private bool _isInitialized = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Properties ────────────────────────────────────────────────
        public ObservableCollection<string> AvailableAdapters { get; } = new();

        public int SelectedAdapterIndex
        {
            get => _networkInterfaces.Count > 0
                ? _networkInterfaces.IndexOf(_selectedInterface ?? _networkInterfaces[0])
                : 0;
            set
            {
                if (value >= 0 && value < _networkInterfaces.Count)
                {
                    var newInterface = _networkInterfaces[value];
                    
                    // Only update if the adapter actually changed
                    if (_selectedInterface?.Name != newInterface.Name)
                    {
                        _selectedInterface = newInterface;
                        _isInitialized = false;  // Force reinitialization for new interface
                        
                        Debug.WriteLine($"Network adapter changed to: {_selectedInterface.Description}");
                        
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(AdapterName));
                        OnPropertyChanged(nameof(NetworkStatusText));
                        
                        // Reset speeds for new adapter
                        UploadSpeed = "↑ 0.0 Mbps";
                        DownloadSpeed = "↓ 0.0 Mbps";
                    }
                }
            }
        }

        public string AdapterName
        {
            get => _adapterName;
            private set { if (_adapterName != value) { _adapterName = value; OnPropertyChanged(); } }
        }

        public string DownloadSpeed
        {
            get => _downloadSpeed;
            private set { if (_downloadSpeed != value) { _downloadSpeed = value; OnPropertyChanged(); } }
        }

        public string UploadSpeed
        {
            get => _uploadSpeed;
            private set { if (_uploadSpeed != value) { _uploadSpeed = value; OnPropertyChanged(); } }
        }

        public string Latency
        {
            get => _latency;
            private set { if (_latency != value) { _latency = value; OnPropertyChanged(); } }
        }

        public string NetworkStatusText
        {
            get
            {
                if (_selectedInterface == null)
                    return "No Network";

                return _selectedInterface.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Ethernet => "Ethernet",
                    NetworkInterfaceType.Wireless80211 => "WiFi",
                    _ => _selectedInterface.NetworkInterfaceType.ToString()
                };
            }
        }

        // ── Constructor ───────────────────────────────────────────────
        public NetworkMonitoringService()
        {
            InitializeNetworkInterfaces();
            InitializeHardwareMonitoring();
        }

        public void StartMonitoring()
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ = MonitorNetworkAsync(_cancellationTokenSource.Token);
            }
        }

        public void StopMonitoring() => _cancellationTokenSource?.Cancel();

        public void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _computer?.Close();
        }

        private void InitializeNetworkInterfaces()
        {
            try
            {
                _networkInterfaces.Clear();
                AvailableAdapters.Clear();

                // Get all network interfaces (both connected and disconnected)
                var allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                 ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ToList();

                // Prioritize connected interfaces, but include all if none are connected
                var connectedInterfaces = allInterfaces
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .ToList();

                _networkInterfaces = connectedInterfaces.Count > 0 ? connectedInterfaces : allInterfaces;

                // Add all interfaces to the dropdown, including status indicator
                foreach (var ni in _networkInterfaces)
                {
                    string status = ni.OperationalStatus == OperationalStatus.Up ? "✓" : "✗";
                    AvailableAdapters.Add($"{status} {ni.Name} ({ni.Description})");
                }

                if (_networkInterfaces.Count > 0)
                {
                    _selectedInterface = _networkInterfaces[0];
                    AdapterName = _selectedInterface.Description;
                }
                else
                {
                    AdapterName = "No Network Adapters";
                }

                Debug.WriteLine($"Found {_networkInterfaces.Count} network interfaces");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing network interfaces: {ex.Message}");
                AdapterName = "Error loading adapters";
            }
        }

        private void InitializeHardwareMonitoring()
        {
            try
            {
                _computer = new Computer
                {
                    IsNetworkEnabled = true,
                };
                _computer.Open();
                Debug.WriteLine("Hardware monitoring initialized for network");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing hardware monitoring: {ex.Message}");
            }
        }

        private async Task MonitorNetworkAsync(CancellationToken cancellationToken)
        {
            bool firstRun = true;
            const double MinElapsedSeconds = 0.3;
            double lastDisplayUpdate = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_selectedInterface == null)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    // Update hardware sensors
                    if (_computer != null)
                    {
                        try
                        {
                            foreach (var hardware in _computer.Hardware)
                            {
                                hardware.Update();
                                foreach (var subHardware in hardware.SubHardware)
                                    subHardware.Update();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Warning: Hardware update failed: {ex.Message}");
                        }
                    }

                    var currentInterface = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(ni => ni.Name == _selectedInterface.Name);

                    if (currentInterface == null)
                    {
                        // Interface was removed, reinitialize
                        InitializeNetworkInterfaces();
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    // Get network statistics
                    long bytesSent = 0;
                    long bytesReceived = 0;

                    try
                    {
                        var stats = currentInterface.GetIPv4Statistics();
                        bytesSent = stats.BytesSent;
                        bytesReceived = stats.BytesReceived;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Warning: Could not get IPv4 stats for {currentInterface.Name}: {ex.Message}");
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    double elapsedSeconds = (now - _lastCheck).TotalSeconds;

                    // Debug dump on first run
                    if (firstRun && _computer != null)
                    {
                        Debug.WriteLine($"=== Network Hardware Available ===");
                        foreach (var hardware in _computer.Hardware)
                        {
                            if (hardware.HardwareType == HardwareType.Network)
                            {
                                Debug.WriteLine($"Network: {hardware.Name}");
                                foreach (var sensor in hardware.Sensors)
                                    Debug.WriteLine($"  [{sensor.SensorType}] {sensor.Name} = {sensor.Value}");
                            }
                        }
                        Debug.WriteLine($"Selected Interface: {currentInterface.Name} ({currentInterface.Description})");
                        Debug.WriteLine($"Initial Stats - BytesSent: {bytesSent}, BytesReceived: {bytesReceived}");
                        firstRun = false;
                    }

                    if (!_isInitialized)
                    {
                        _lastBytesSent = bytesSent;
                        _lastBytesReceived = bytesReceived;
                        _lastCheck = now;
                        _isInitialized = true;
                        UploadSpeed = "↑ 0.0 Mbps";
                        DownloadSpeed = "↓ 0.0 Mbps";
                        Debug.WriteLine($"Network monitoring initialized for: {currentInterface.Description}");
                    }
                    else if (elapsedSeconds >= MinElapsedSeconds)
                    {
                        double uploadMbps = 0;
                        double downloadMbps = 0;

                        // Calculate speed from byte deltas
                        long bytesSentDelta = bytesSent - _lastBytesSent;
                        long bytesReceivedDelta = bytesReceived - _lastBytesReceived;

                        if (elapsedSeconds > 0)
                        {
                            // Convert bytes to bits, then to megabits per second
                            // Formula: (bytes * 8 bits/byte) / (1,000,000 bits/Mbps) / elapsed_seconds
                            uploadMbps = (bytesSentDelta * 8.0) / (100_000_0000.0 * elapsedSeconds);
                            downloadMbps = (bytesReceivedDelta * 8.0) / (100_000_000.0 * elapsedSeconds);
                        }

                        // Try hardware monitor as secondary source if available
                        float? hardwareUploadSpeed = GetNetworkSpeedFromHardware(true);
                        float? hardwareDownloadSpeed = GetNetworkSpeedFromHardware(false);

                        // Use hardware speed if it's significantly different (indicates active monitoring)
                        if (hardwareUploadSpeed.HasValue && hardwareUploadSpeed.Value > 0)
                        {
                            uploadMbps = hardwareUploadSpeed.Value;
                        }

                        if (hardwareDownloadSpeed.HasValue && hardwareDownloadSpeed.Value > 0)
                        {
                            downloadMbps = hardwareDownloadSpeed.Value;
                        }

                        // Cap at realistic values (10 Gbps is more common than 100 Gbps)
                        const double MaxRealisticSpeed = 10_000; // 10 Gbps
                        uploadMbps = Math.Min(uploadMbps, MaxRealisticSpeed);
                        downloadMbps = Math.Min(downloadMbps, MaxRealisticSpeed);

                        // Clamp to non-negative
                        uploadMbps = Math.Max(uploadMbps, 0);
                        downloadMbps = Math.Max(downloadMbps, 0);

                        // Update UI
                        string newUploadSpeed = $"↑ {uploadMbps:F1} Kbps";
                        string newDownloadSpeed = $"↓ {downloadMbps:F1} Kbps";

                        UploadSpeed = newUploadSpeed;
                        DownloadSpeed = newDownloadSpeed;

                        // Log detailed stats
                        lastDisplayUpdate += elapsedSeconds;
                        if (lastDisplayUpdate >= 1.0 || uploadMbps > 0 || downloadMbps > 0)
                        {
                            Debug.WriteLine($"[{currentInterface.Description}] Upload: {uploadMbps:F1} Mbps | Download: {downloadMbps:F1} Mbps | Elapsed: {elapsedSeconds:F2}s | Sent: {bytesSent} ({bytesSentDelta} delta) | Received: {bytesReceived} ({bytesReceivedDelta} delta)");
                            lastDisplayUpdate = 0;
                        }

                        _lastBytesSent = bytesSent;
                        _lastBytesReceived = bytesReceived;
                        _lastCheck = now;
                    }

                    OnPropertyChanged(nameof(NetworkStatusText));
                    OnPropertyChanged(nameof(AdapterName));

                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error monitoring network: {ex.Message} | StackTrace: {ex.StackTrace}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Retrieves network speed from LibreHardwareMonitor hardware sensors.
        /// Returns null if the data is unavailable.
        /// </summary>
        private float? GetNetworkSpeedFromHardware(bool isUpload)
        {
            if (_computer == null || _selectedInterface == null)
                return null;

            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Network)
                        continue;

                    // Try to match hardware with selected interface
                    if (!IsNetworkHardwareMatch(hardware, _selectedInterface))
                        continue;

                    // First priority: specific upload/download sensors
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Throughput || sensor.Value == null)
                            continue;

                        string sensorNameLower = sensor.Name.ToLower();

                        if (isUpload && (sensorNameLower.Contains("upload") || sensorNameLower.Contains("tx") || sensorNameLower.Contains("transmit")))
                        {
                            return sensor.Value >= 0 ? sensor.Value : null;
                        }

                        if (!isUpload && (sensorNameLower.Contains("download") || sensorNameLower.Contains("rx") || sensorNameLower.Contains("receive")))
                        {
                            return sensor.Value >= 0 ? sensor.Value : null;
                        }
                    }

                    // Second priority: look for general throughput sensors if no specific ones found
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Throughput || sensor.Value == null)
                            continue;

                        string sensorNameLower = sensor.Name.ToLower();

                        // Match any throughput sensor
                        if (sensorNameLower.Contains("throughput") || sensorNameLower.Contains("speed"))
                        {
                            return sensor.Value >= 0 ? sensor.Value : null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading network hardware speeds: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Checks if the hardware sensor matches the selected network interface.
        /// </summary>
        private static bool IsNetworkHardwareMatch(IHardware hardware, NetworkInterface ni)
        {
            string hwNameLower = hardware.Name.ToLower();
            string niNameLower = ni.Name.ToLower();
            string niDescLower = ni.Description.ToLower();

            // Try multiple matching strategies
            return hwNameLower.Contains(niNameLower) || 
                   hwNameLower.Contains(niDescLower) ||
                   niNameLower.Contains(hwNameLower) ||
                   niDescLower.Contains(hwNameLower) ||
                   // Also try matching by common terms
                   (hwNameLower.Contains("ethernet") && niDescLower.Contains("ethernet")) ||
                   (hwNameLower.Contains("wifi") && niDescLower.Contains("wifi")) ||
                   (hwNameLower.Contains("wireless") && niDescLower.Contains("wireless"));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}