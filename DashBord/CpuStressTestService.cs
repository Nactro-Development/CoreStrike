using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace CoreStrike.DashBord
{
    public sealed class CpuStressTestService : INotifyPropertyChanged
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning;
        private Stopwatch? _stopwatch;
        private long _iterationCount;
        private CpuMonitoringService? _cpuService;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<StressTestSummary>? TestCompleted;

        public CpuStressTestService(CpuMonitoringService cpuService)
        {
            _cpuService = cpuService;
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set { if (_isRunning != value) { _isRunning = value; OnPropertyChanged(); } }
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _stopwatch = Stopwatch.StartNew();
            _iterationCount = 0;
            IsRunning = true;

            // එක core එකක් OS + UI එකට leave කරන්න
            int threadCount = Math.Max(1, Environment.ProcessorCount - 1);

            for (int i = 0; i < threadCount; i++)
            {
                var token = _cts.Token;
                var thread = new Thread(() =>
                {
                    long localCounter = 0;
                    while (!token.IsCancellationRequested)
                    {
                        localCounter++;
                        if (localCounter % 10_000_000 == 0)
                            Interlocked.Increment(ref _iterationCount);
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal // Highest වෙනුවට AboveNormal — OS freeze වෙන්නේ නෑ
                };
                thread.Start();
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _stopwatch?.Stop();
            IsRunning = false;

            // Raise event with summary
            if (_stopwatch != null)
            {
                var summary = new StressTestSummary
                {
                    Duration = _stopwatch.Elapsed,
                    ThreadCount = Environment.ProcessorCount,
                    IterationCount = Interlocked.Read(ref _iterationCount),
                    CompletionTime = DateTime.Now,
                    MaxCpuTemperature = _cpuService?.MaxCpuTemperature ?? 0
                };

                TestCompleted?.Invoke(this, summary);
            }
        }

        public void Toggle()
        {
            if (IsRunning) Stop();
            else Start();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StressTestSummary
    {
        public TimeSpan Duration { get; set; }
        public int ThreadCount { get; set; }
        public long IterationCount { get; set; }
        public DateTime CompletionTime { get; set; }
        public double MaxCpuTemperature { get; set; }

        public override string ToString()
        {
            return $"Duration: {Duration:hh\\:mm\\:ss}\n" +
                   $"Threads: {ThreadCount}\n" +
                   $"Iterations: {IterationCount:N0}\n" +
                   $"Completed: {CompletionTime:yyyy-MM-dd HH:mm:ss}\n" +
                   $"Max Temperature: {MaxCpuTemperature:F1}°C";
        }
    }
}