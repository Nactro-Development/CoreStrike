using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;

using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ObservableCollections = System.Collections.ObjectModel;

namespace CoreStrike.DashBord
{
    public sealed partial class DashBord : Page, INotifyPropertyChanged
    {
        private CpuMonitoringService? _cpuService;
        private GpuMonitoringService? _gpuService;
        private MotherboardMonitoringService? _mbService;
        private MemoryMonitoringService? _memService;
        private NetworkMonitoringService? _networkService;
        private ProcessMonitoringService? _processService;
        public static event Action DashboardReady;

        public event PropertyChangedEventHandler? PropertyChanged;
        private StorageMonitoringService? _storageService;

        private CpuStressTestService _stressTest;

        public bool IsStressTesting => _stressTest.IsRunning;
        public string StressButtonText => _stressTest.IsRunning ? "Stop Stress Test" : "Stress Test";

        public IEnumerable<ISeries> CpuSeries { get; set; }
        public IEnumerable<ISeries> Gpu3DSeries { get; set; }
        public IEnumerable<ISeries> Gpu3DCopySeries { get; set; }
        public IEnumerable<ISeries> Gpu3DVESeries { get; set; }
        public IEnumerable<ISeries> Gpu3DVDSeries { get; set; }
        public IEnumerable<ISeries> RAMSeries { get; set; }
        public IEnumerable<ISeries> VRAMSeries { get; set; }

        public IEnumerable<ICartesianAxis> XAxes { get; set; }
        public IEnumerable<ICartesianAxis> YAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DYAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DCopyXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DCopyYAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVEXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVEYAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVDXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVDYAxes { get; set; }
        public IEnumerable<ICartesianAxis> RAMXAxes { get; set; }
        public IEnumerable<ICartesianAxis> RAMYAxes { get; set; }
        public IEnumerable<ICartesianAxis> VRAMXAxes { get; set; }
        public IEnumerable<ICartesianAxis> VRAMYAxes { get; set; }

        // ── CPU Properties ────────────────────────────────────
        public string CpuName => _cpuService?.CpuName ?? string.Empty;
        public string CpuSpeed => _cpuService?.CpuSpeed ?? "0 MHz";
        public string CpuTemperature => _cpuService?.CpuTemperature ?? "0°C";
        public string CpuDisplayText => _cpuService?.CpuDisplayText ?? string.Empty;
        public string CpuCoresAvg => _cpuService?.CpuCoresAvg ?? "N/A";
        public string CpuPackagePower => _cpuService?.CpuPackagePower ?? "N/A";
        public string CpuCoreSvi2Voltage => _cpuService?.CpuCoreSvi2Voltage ?? "N/A";
        public string CpuBusSpeed => _cpuService?.CpuBusSpeed ?? "N/A";

        // ── GPU Properties ────────────────────────────────────
        public string GpuName => _gpuService?.GpuName ?? string.Empty;
        public string GpuClock => _gpuService?.GpuClock ?? "0 MHz";
        public string GpuTemperature => _gpuService?.GpuTemperature ?? "0°C";
        public string GpuDisplayText => _gpuService?.GpuDisplayText ?? string.Empty;
        public string Gpu3DCopyDisplayText => _gpuService?.Gpu3DCopyDisplayText ?? string.Empty;
        public string Gpu3DVEDisplayText => _gpuService?.Gpu3DVEDisplayText ?? string.Empty;
        public string Gpu3DVDDisplayText => _gpuService?.Gpu3DVDDisplayText ?? string.Empty;
        public string GpuMemoryUsed => _gpuService?.GpuMemoryUsed ?? "0 MB";
        public string GpuMemoryTotal => _gpuService?.GpuMemoryTotal ?? "0 MB";
        public string FanDisplayText => _gpuService?.GPUfanText ?? "N/A";
        public string GPUPowerText => _gpuService?.GPUPowerText ?? "N/A";
        public string GPUCoreUsageText => _gpuService?.GPUCoreUsageText ?? "N/A";

        public List<string> AvailableGpus => _gpuService?.AvailableGpus ?? new();
        public List<string> AvailableDrives => _storageService?.AvailableDrives ?? new();


        public int SelectedDriveIndex
        {
            get => _storageService?.SelectedDriveIndex ?? 0;
            set
            {
                if (_storageService != null && _storageService.SelectedDriveIndex != value)
                {
                    _storageService.SelectedDriveIndex = value;
                    OnPropertyChanged();
                }
            }
        }



        public int SelectedGpuIndex
        {
            get => _gpuService?.SelectedGpuIndex ?? 0;
            set
            {
                if (_gpuService != null && _gpuService.SelectedGpuIndex != value)
                {
                    _gpuService.SelectedGpuIndex = value;
                    OnPropertyChanged();
                }
            }
        }


        public string DriveName => _storageService?.DriveName ?? string.Empty;
        public string DriveModel => _storageService?.DriveModel ?? string.Empty;
        public string DriveType => _storageService?.DriveType ?? "N/A";
        public string DriveHealth => _storageService?.DriveHealth ?? "N/A";
        public string DriveTemperature => _storageService?.DriveTemperature ?? "N/A";
        public string DriveUsedSpace => _storageService?.DriveUsedSpace ?? "N/A";
        public string DriveFreeSpace => _storageService?.DriveFreeSpace ?? "N/A";
        public string DriveTotalSpace => _storageService?.DriveTotalSpace ?? "N/A";
        public string DriveUsageText => _storageService?.DriveUsageText ?? "N/A";
        public string DriveReadRate => _storageService?.DriveReadRate ?? "N/A";
        public string DriveWriteRate => _storageService?.DriveWriteRate ?? "N/A";
        public string DriveThroughput => _storageService?.DriveThroughput ?? "N/A";
        public string DriveLifeText => _storageService?.DriveLifeText ?? "N/A";
        public string PowerOnHours => _storageService?.PowerOnHours ?? "N/A";
        public float DriveUsagePercent => _storageService?.DriveUsagePercent ?? 0f;


        // ── Motherboard Properties ────────────────────────────
        public string CpufanText => _mbService?.Fan1Rpm ?? string.Empty;

        // ── Memory Properties ─────────────────────────────────
        public string MemoryUsed => _memService?.MemoryUsed ?? "N/A";
        public string MemoryAvailable => _memService?.MemoryAvailable ?? "N/A";
        public string MemoryTotal => _memService?.MemoryTotal ?? "N/A";
        public string MemoryUsageText => _memService?.MemoryUsageText ?? "0%";
        public string MemoryLoadText => _memService?.MemoryLoadText ?? "Usage: 0%";
        public string VirtualUsed => _memService?.VirtualUsed ?? "N/A";
        public string VirtualUsage => _memService?.VirtualUsage ?? "N/A";

        public string VirtualTotal => _memService?.VirtualTotal ?? "N/A";
        public float MemoryUsagePercent => _memService?.MemoryUsagePercent ?? 0f;
        public float MaxMemoryUsage => _memService?.MaxMemoryUsage ?? 0f;

        // ── Network Properties ─────────────────────────────────
        public ObservableCollections.ObservableCollection<string> AvailableNetworkAdapters => _networkService?.AvailableAdapters ?? new();

        public int SelectedNetworkAdapterIndex
        {
            get => _networkService?.SelectedAdapterIndex ?? 0;
            set
            {
                if (_networkService != null && _networkService.SelectedAdapterIndex != value)
                {
                    _networkService.SelectedAdapterIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NetworkAdapterName => _networkService?.AdapterName ?? "Not Connected";
        public string NetworkDownloadSpeed => _networkService?.DownloadSpeed ?? "↓ 0.0 Mbps";
        public string NetworkUploadSpeed => _networkService?.UploadSpeed ?? "↑ 0.0 Mbps";
        public string NetworkLatency => _networkService?.Latency ?? "Latency: 0 ms";
        public string NetworkStatus => _networkService?.NetworkStatusText ?? "No Network";

        // ── Process Properties ─────────────────────────────────
        public ObservableCollections.ObservableCollection<ProcessInfo> TopProcesses => _processService?.TopProcesses ?? new();
        public float ProcessCpuUsageTotal => _processService?.TotalCpuUsage ?? 0f;

        // ── Constructor ───────────────────────────────────────
        public DashBord()
        {

            this.NavigationCacheMode =
       Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            InitializeComponent();

            _cpuService = new CpuMonitoringService();
            _gpuService = new GpuMonitoringService();
            _mbService = new MotherboardMonitoringService();
            // ✅ FIX: _memService ව InitializeChart() කලින් new කරන්න
            _memService = new MemoryMonitoringService();
            _storageService = new StorageMonitoringService();
            _networkService = new NetworkMonitoringService();
            _processService = new ProcessMonitoringService(); // Initialize ProcessMonitoringService
            
            // Set up UI dispatcher for process monitoring service
            _processService.SetUIDispatcher(action =>
            {
                this.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => action());
                return true;
            });
            // ✅ InitializeChart() call කරනහැටි සියලු services ready වෙලා
            InitializeChart();

            _storageService.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(StorageMonitoringService.AvailableDrives))
                    OnPropertyChanged(nameof(AvailableDrives));
            };


            // PropertyChanged wiring
            _cpuService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            _gpuService.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(GpuMonitoringService.AvailableGpus))
                    OnPropertyChanged(nameof(AvailableGpus));
            };
            _mbService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MotherboardMonitoringService.Fan1Rpm))
                    OnPropertyChanged(nameof(CpufanText));
            };
            _memService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            _networkService.PropertyChanged += (s, e) =>
            {
                // Propagate all network service property changes to the UI
                OnPropertyChanged(e.PropertyName);
                
                // Also update the proxy properties when relevant network properties change
                if (e.PropertyName == nameof(NetworkMonitoringService.DownloadSpeed))
                    OnPropertyChanged(nameof(NetworkDownloadSpeed));
                else if (e.PropertyName == nameof(NetworkMonitoringService.UploadSpeed))
                    OnPropertyChanged(nameof(NetworkUploadSpeed));
                else if (e.PropertyName == nameof(NetworkMonitoringService.AdapterName))
                    OnPropertyChanged(nameof(NetworkAdapterName));
                else if (e.PropertyName == nameof(NetworkMonitoringService.Latency))
                    OnPropertyChanged(nameof(NetworkLatency));
                else if (e.PropertyName == nameof(NetworkMonitoringService.NetworkStatusText))
                    OnPropertyChanged(nameof(NetworkStatus));
                else if (e.PropertyName == nameof(NetworkMonitoringService.AvailableAdapters))
                    OnPropertyChanged(nameof(AvailableNetworkAdapters));
            };
            _processService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

            // Start all services
            _cpuService.StartMonitoring();
            _gpuService.StartMonitoring();
            _mbService.StartMonitoring();
            _memService.StartMonitoring();
            _storageService.StartMonitoring();
            _networkService.StartMonitoring();
            _processService.StartMonitoring(); // Start monitoring processes


            // Stress test
            _stressTest = new CpuStressTestService(_cpuService);
            _stressTest.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsStressTesting));
                OnPropertyChanged(nameof(StressButtonText));
            };
            _stressTest.TestCompleted += (s, summary) => ShowStressTestSummary(summary);

       

            StartFan();
            StartFanGPU();

            DashBord_Loaded();
        }







        private void DashBord_Loaded()
        {
            DashboardReady?.Invoke();
        }



        // ── Chart Init ────────────────────────────────────────
        private void InitializeChart()
        {
            // CPU
            var cpuXAxis = MakeXAxis();
            XAxes = new[] { cpuXAxis };
            YAxes = new[] { MakeYAxis() };

            // GPU 3D
            var gpu3DXAxis = MakeXAxis();
            Gpu3DXAxes = new[] { gpu3DXAxis };
            Gpu3DYAxes = new[] { MakeYAxis() };

            // GPU Copy
            var gpu3DCopyXAxis = MakeXAxis();
            Gpu3DCopyXAxes = new[] { gpu3DCopyXAxis };
            Gpu3DCopyYAxes = new[] { MakeYAxis() };

            // GPU VE
            var gpu3DVEXAxis = MakeXAxis();
            Gpu3DVEXAxes = new[] { gpu3DVEXAxis };
            Gpu3DVEYAxes = new[] { MakeYAxis() };

            // GPU VD
            var gpu3DVDXAxis = MakeXAxis();
            Gpu3DVDXAxes = new[] { gpu3DVDXAxis };
            Gpu3DVDYAxes = new[] { MakeYAxis() };

            // RAM
            var ramXAxis = MakeXAxis();
            RAMXAxes = new[] { ramXAxis };
            RAMYAxes = new[] { MakeYAxis() };

            // VRAM
            var VramXAxis = MakeXAxis();
            VRAMXAxes = new[] { VramXAxis };
            VRAMYAxes = new[] { MakeYAxis() };

            // Series
            CpuSeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _cpuService?.CpuUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            Gpu3DSeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _gpuService?.GpuUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            Gpu3DCopySeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _gpuService?.Gpu3DCopyUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            Gpu3DVESeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _gpuService?.Gpu3DVEUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            Gpu3DVDSeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _gpuService?.Gpu3DVEDUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            // ✅ FIX: _memService දැන් null නෑ — InitializeChart() කලින් new කලා
            RAMSeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _memService?.MemoryUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            // ✅ FIX: _memService දැන් null නෑ — InitializeChart() කලින් new කලා
            VRAMSeries = new[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _memService?.VMemoryUsageData,
                    Fill   = new SolidColorPaint(GetAccentSKColor(128)),
                    Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
                    GeometrySize  = 0,
                    LineSmoothness = 0.5,
                }
            };

            // Axis wiring
            _cpuService?.SetXAxis(cpuXAxis);
            _gpuService?.SetXAxis(gpu3DXAxis);
            _gpuService?.Set3DCopyXAsis(gpu3DCopyXAxis);
            _gpuService?.Set3DVEXAsis(gpu3DVEXAxis);
            _gpuService?.Set3DVDXAsis(gpu3DVDXAxis);
            // ✅ FIX: RAM axis wiring — කලන්
            _memService?.SetXAxis(ramXAxis);
            _memService?.SetVXAxis(VramXAxis);
        }

        // ── Axis helpers ──────────────────────────────────────
        private static Axis MakeXAxis() => new Axis
        {
            MinLimit = 0,
            MaxLimit = 49,
            IsVisible = false,
            SeparatorsAtCenter = true,
            ShowSeparatorLines = false,
        };

        private static Axis MakeYAxis() => new Axis
        {
            IsVisible = false,
            MinLimit = 0,
            MaxLimit = 100,
            ShowSeparatorLines = false,
        };

        // ── Stress Test ───────────────────────────────────────
        private void StressTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_stressTest.IsRunning)
            {
                _stressTest.Stop();
            }
            else
            {
                _cpuService?.ResetMaxTemperature();
                _stressTest.Start();
            }
        }

        private async void ShowStressTestSummary(StressTestSummary summary)
        {
            var panel = new StackPanel { Spacing = 12 };

            var statsGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statsGrid.RowDefinitions.Add(new RowDefinition());
            statsGrid.RowDefinitions.Add(new RowDefinition());
            statsGrid.RowDefinitions.Add(new RowDefinition());

            var durationCard = CreateStatCard("DURATION",
                $"{(int)summary.Duration.TotalMinutes}m {summary.Duration.Seconds}s");
            var threadsCard = CreateStatCard("THREADS USED", summary.ThreadCount.ToString());
            var iterCard = CreateStatCard("ITERATIONS", $"{summary.IterationCount:N0}");
            var timeCard = CreateStatCard("COMPLETED", summary.CompletionTime.ToString("HH:mm:ss"));
            var tempCard = CreateStatCard("MAX TEMPERATURE", $"{summary.MaxCpuTemperature:F1}°C");

            Grid.SetColumn(durationCard, 0); Grid.SetRow(durationCard, 0);
            Grid.SetColumn(threadsCard, 1); Grid.SetRow(threadsCard, 0);
            Grid.SetColumn(iterCard, 0); Grid.SetRow(iterCard, 1);
            Grid.SetColumn(timeCard, 1); Grid.SetRow(timeCard, 1);
            Grid.SetColumn(tempCard, 0); Grid.SetRow(tempCard, 2);
            Grid.SetColumnSpan(tempCard, 2);

            statsGrid.Children.Add(durationCard);
            statsGrid.Children.Add(threadsCard);
            statsGrid.Children.Add(iterCard);
            statsGrid.Children.Add(timeCard);
            statsGrid.Children.Add(tempCard);
            panel.Children.Add(statsGrid);

            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Success,
                Message = "All threads stopped cleanly. No thermal throttling detected."
            });

            var dialog = new ContentDialog
            {
                Title = "Stress test complete",
                Content = panel,
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private Border CreateStatCard(string label, string value)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 38, 38, 38)),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text       = label,
                            FontSize   = 10,
                            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 102, 102, 102))
                        },
                        new TextBlock
                        {
                            Text       = value,
                            FontSize   = 22,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold
                        }
                    }
                }
            };
        }

        // ── Helpers ───────────────────────────────────────────
        private SKColor GetAccentSKColor(byte alpha = 255)
        {
            var accent = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
            return new SKColor(accent.R, accent.G, accent.B, alpha);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ── Fan Animations ────────────────────────────────────
        private void StartFan()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, FanRotate);
            Storyboard.SetTargetProperty(animation, "Angle");
            storyboard.Begin();
        }

        private void StartFanGPU()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, FanRotateGPU);
            Storyboard.SetTargetProperty(animation, "Angle");
            storyboard.Begin();
        }
    }
}