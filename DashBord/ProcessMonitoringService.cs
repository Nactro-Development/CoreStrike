using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreStrike.DashBord
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private double _cpuUsage;
        private double _memoryUsage;
        private string _displayName = string.Empty;

        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.01)
                {
                    _cpuUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                if (Math.Abs(_memoryUsage - value) > 0.01)
                {
                    _memoryUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CpuUsageText => $"{CpuUsage:F1}%";
        public string MemoryUsageText => $"{MemoryUsage:F1}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProcessMonitoringService : INotifyPropertyChanged
    {
        private ObservableCollection<ProcessInfo> _topProcesses;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitoringTask;
        private bool _isMonitoring;
        private float _totalCpuUsage;
        private Process? _currentProcess;
        private DateTime _lastCpuCheck = DateTime.UtcNow;
        private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
        private Func<Action, bool>? _uiDispatcher;

        public ObservableCollection<ProcessInfo> TopProcesses
        {
            get => _topProcesses;
            set
            {
                if (_topProcesses != value)
                {
                    _topProcesses = value;
                    OnPropertyChanged();
                }
            }
        }

        public float TotalCpuUsage
        {
            get => _totalCpuUsage;
            set
            {
                if (Math.Abs(_totalCpuUsage - value) > 0.01)
                {
                    _totalCpuUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        public ProcessMonitoringService()
        {
            _topProcesses = new ObservableCollection<ProcessInfo>();
            _currentProcess = Process.GetCurrentProcess();
            _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
        }

        /// <summary>
        /// Set the UI dispatcher callback for thread-safe UI updates
        /// </summary>
        public void SetUIDispatcher(Func<Action, bool> dispatcher)
        {
            _uiDispatcher = dispatcher;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = MonitoringLoop(_cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _cancellationTokenSource?.Cancel();
            try
            {
                _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Handle timeout
            }
        }

         private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Run(() => UpdateProcessData(), cancellationToken);
                    await Task.Delay(3000, cancellationToken); // Update every 3 seconds (reduced frequency)
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        private void UpdateProcessData()
        {
            try
            {
                var processes = Process.GetProcesses();

                // Calculate CPU usage for each process
                var processDataList = new System.Collections.Generic.List<ProcessInfo>();

                foreach (var process in processes)
                {
                    try
                    {
                        var cpuUsage = CalculateProcessCpuUsage(process);
                        var memoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0);
                        var memoryPercent = (memoryUsageMB / (Environment.ProcessorCount * 1024.0)) * 100;

                        if (cpuUsage > 0 || memoryPercent > 0.5)
                        {
                            var processName = process.ProcessName;
                            var processInfo = new ProcessInfo
                            {
                                ProcessName = processName,
                                ProcessId = process.Id,
                                DisplayName = $"{processName}",
                                CpuUsage = cpuUsage,
                                MemoryUsage = memoryPercent
                            };

                            processDataList.Add(processInfo);
                        }
                    }
                    catch
                    {
                        // Skip processes that throw errors
                    }
                }

                // Get top 5 processes by CPU usage
                var topProcesses = processDataList
                    .OrderByDescending(p => p.CpuUsage)
                    .Take(5)
                    .ToList();

                // Update UI collection using dispatcher callback if available
                if (_uiDispatcher != null)
                {
                    _uiDispatcher(() =>
                    {
                        TopProcesses.Clear();
                        foreach (var process in topProcesses)
                        {
                            TopProcesses.Add(process);
                        }

                        // Calculate and update total CPU usage
                        TotalCpuUsage = (float)topProcesses.Sum(p => p.CpuUsage);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Process monitoring error: {ex.Message}");
            }
        }

        private double CalculateProcessCpuUsage(Process process)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var currentTotalProcessorTime = process.TotalProcessorTime;

                var cpuUsedMs = (currentTotalProcessorTime - (_lastTotalProcessorTime)).TotalMilliseconds;
                var totalMsPassed = (currentTime - _lastCpuCheck).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var cpuUsage = cpuUsageTotal * 100;

                return Math.Max(0, cpuUsage);
            }
            catch
            {
                return 0;
            }
        }

        public void Cleanup()
        {
            StopMonitoring();
            _currentProcess?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
