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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoreStrike.DashBord
{
    public sealed partial class DashBord : Page, INotifyPropertyChanged
    {
        private CpuMonitoringService? _cpuService;
        private GpuMonitoringService? _gpuService;
        private MotherboardMonitoringService? _mbService;

        public event PropertyChangedEventHandler? PropertyChanged;
        // Field add කරන්න
        private CpuStressTestService _stressTest;

        public bool IsStressTesting => _stressTest.IsRunning;

        public string StressButtonText => _stressTest.IsRunning ? "Stop Stress Test" : "Stress Test";

        public IEnumerable<ISeries> CpuSeries { get; set; }
        public IEnumerable<ISeries> Gpu3DSeries { get; set; }
        public IEnumerable<ISeries> Gpu3DCopySeries { get; set; }
        public IEnumerable<ISeries> Gpu3DVESeries { get; set; }
        public IEnumerable<ISeries> Gpu3DVDSeries { get; set; }
        public IEnumerable<ICartesianAxis> XAxes { get; set; }
        public IEnumerable<ICartesianAxis> YAxes { get; set; }

        public string CpuName
        {
            get => _cpuService?.CpuName ?? string.Empty;
        }

        public string CpuSpeed
        {
            get => _cpuService?.CpuSpeed ?? "0 MHz";
        }

        public string CpuTemperature
        {
            get => _cpuService?.CpuTemperature ?? "0°C";
        }

        public string CpuDisplayText
        {
            get => _cpuService?.CpuDisplayText ?? string.Empty;
        }

        public string CpuCoresAvg
        {
            get => _cpuService?.CpuCoresAvg ?? "N/A";
        }


        public string CpuPackagePower => _cpuService?.CpuPackagePower ?? "N/A";
        public string CpuCoreSvi2Voltage => _cpuService?.CpuCoreSvi2Voltage ?? "N/A";
        public string CpuSocSvi2Voltage => _cpuService?.CpuSocSvi2Voltage ?? "N/A";
        public string GPUfanText => _gpuService?.GPUfanText ?? "N/A";
        public string GPUPowerText => _gpuService?.GPUPowerText ?? "N/A";
        public string GPUCoreUsageText => _gpuService?.GPUCoreUsageText ?? "N/A";




        public string GpuName
        {
            get => _gpuService?.GpuName ?? string.Empty;
        }

        public string GpuClock
        {
            get => _gpuService?.GpuClock ?? "0 MHz";
        }

        public string GpuTemperature
        {
            get => _gpuService?.GpuTemperature ?? "0°C";
        }

        public string GpuDisplayText
        {
            get => _gpuService?.GpuDisplayText ?? string.Empty;
        }
        
        public string Gpu3DCopyDisplayText
        {
            get => _gpuService?.Gpu3DCopyDisplayText ?? string.Empty;
        }


        public string CpufanText
        {
            get => _mbService?.Fan1Rpm ?? string.Empty;
        }
        public string Gpu3DVEDisplayText
        {
            get => _gpuService?.Gpu3DVEDisplayText ?? string.Empty;
        }
        public string Gpu3DVDDisplayText
        {
            get => _gpuService?.Gpu3DVDDisplayText ?? string.Empty;
        }

        public string GpuMemoryUsed
        {
            get => _gpuService?.GpuMemoryUsed ?? "0 MB";
        }

        public string GpuMemoryTotal
        {
            get => _gpuService?.GpuMemoryTotal ?? "0 MB";
        }

        public DashBord()
        {
            InitializeComponent();
            _cpuService = new CpuMonitoringService();
            _gpuService = new GpuMonitoringService();
            _mbService = new MotherboardMonitoringService(); // ✅ ADD

            InitializeChart();
            _cpuService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            _gpuService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            _cpuService.StartMonitoring();
            _gpuService.StartMonitoring();
            _mbService.StartMonitoring();


            _stressTest = new CpuStressTestService(_cpuService);  // Pass service to stress test

            _stressTest.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsStressTesting));
                OnPropertyChanged(nameof(StressButtonText));
            };

            // ✅ ADD - මේක නැතිව CpufanText update වෙන්නෑ
            _mbService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MotherboardMonitoringService.Fan1Rpm))
                    OnPropertyChanged(nameof(CpufanText));
            };


            _stressTest.TestCompleted += (s, summary) => ShowStressTestSummary(summary);
      


            Unloaded += (s, e) =>
            {
                _cpuService?.Cleanup();
                _gpuService?.Cleanup();
                _mbService?.Cleanup(); // ✅ ADD
                _stressTest.Stop();
            };
            StartFan();
            StartFanGPU();
        }

        private void StressTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_stressTest.IsRunning)
            {
                _stressTest.Stop();
            }
            else
            {
                _cpuService?.ResetMaxTemperature();  // Reset before test starts
                _stressTest.Start();
            }

        }




        private async void ShowStressTestSummary(StressTestSummary summary)
        {
            var panel = new StackPanel { Spacing = 12 };

            // Stats grid
            var statsGrid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };
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

            // Status bar
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

            var result = await dialog.ShowAsync();

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

        private SKColor GetAccentSKColor(byte alpha = 255)
        {
            var accent = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
            return new SKColor(accent.R, accent.G, accent.B, alpha);
        }

        private void InitializeChart()
        {
            // ── CPU Axes ──────────────────────────────────────
            var cpuXAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            var cpuYAxis = new Axis
            {
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = 100,
                ShowSeparatorLines = false,
            };


            XAxes = new[] { cpuXAxis };
            YAxes = new[] { cpuYAxis };



            // ── GPU3D Axes ──────────────────────────────────────
            var gpu3DXAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            var gpu3DYAxis = new Axis
            {
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = 100,
                ShowSeparatorLines = false,
            };


            Gpu3DXAxes = new[] { gpu3DXAxis };
            Gpu3DYAxes = new[] { gpu3DYAxis };




            // ── GPU3DCopy Axes ──────────────────────────────────────
            var gpu3DCopyXAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            var gpu3DCopyYAxis = new Axis
            {
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = 100,
                ShowSeparatorLines = false,
            };


            Gpu3DCopyXAxes = new[] { gpu3DCopyXAxis };
            Gpu3DCopyYAxes = new[] { gpu3DCopyYAxis };




            // ── GPU3DVE Axes ──────────────────────────────────────
            var gpu3DVEXAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            var gpu3DVEYAxis = new Axis
            {
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = 100,
                ShowSeparatorLines = false,
            };


            Gpu3DVEXAxes = new[] { gpu3DVEXAxis };
            Gpu3DVEYAxes = new[] { gpu3DVEYAxis };


            // ── GPU3DVD Axes ──────────────────────────────────────
            var gpu3DVDXAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            var gpu3DVDYAxis = new Axis
            {
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = 100,
                ShowSeparatorLines = false,
            };


            Gpu3DVDXAxes = new[] { gpu3DVDXAxis };
            Gpu3DVDYAxes = new[] { gpu3DVDYAxis };











            // Series එකක් හදන්න
            CpuSeries = new[]
            {
        new LineSeries<ObservablePoint>
        {
            Values = _cpuService?.CpuUsageData,
            Fill = new SolidColorPaint(GetAccentSKColor(128)),
            Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
            GeometrySize = 0,
            LineSmoothness = 0.5,
        }
    };

            Gpu3DSeries = new[]
            {
        new LineSeries<ObservablePoint>
        {
            Values = _gpuService?.GpuUsageData,
            Fill = new SolidColorPaint(GetAccentSKColor(128)),
            Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
            GeometrySize = 0,
            LineSmoothness = 0.5,
        }
    };


            // ✅ නිවැරදි - GPU 3D Copy data use කරනවා
            Gpu3DCopySeries = new[]
            {
    new LineSeries<ObservablePoint>
    {
        Values = _gpuService?.Gpu3DCopyUsageData,  // <-- මේක
        Fill = new SolidColorPaint(GetAccentSKColor(128)),
        Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
        GeometrySize = 0,
        LineSmoothness = 0.5,
    }
};
            
            // ✅ නිවැරදි - GPU 3D Copy data use කරනවා
            Gpu3DVESeries = new[]
            {
    new LineSeries<ObservablePoint>
    {
        Values = _gpuService?.Gpu3DVEUsageData,  // <-- මේක
        Fill = new SolidColorPaint(GetAccentSKColor(128)),
        Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
        GeometrySize = 0,
        LineSmoothness = 0.5,
    }
};
            // ✅ නිවැරදි - GPU 3D Copy data use කරනවා
            Gpu3DVDSeries = new[]
            {
    new LineSeries<ObservablePoint>
    {
        Values = _gpuService?.Gpu3DVEDUsageData,  // <-- මේක
        Fill = new SolidColorPaint(GetAccentSKColor(128)),
        Stroke = new SolidColorPaint(GetAccentSKColor(255)) { StrokeThickness = 2 },
        GeometrySize = 0,
        LineSmoothness = 0.5,
    }
};


            // වෙනම axis services වලට pass කරන්න
            _cpuService?.SetXAxis(cpuXAxis);

            // InitializeChart() ඇතුළේ මේකත් add කරන්න
            _gpuService?.Set3DCopyXAsis(gpu3DCopyXAxis);
            _gpuService?.Set3DVEXAsis(gpu3DVEXAxis);
            _gpuService?.Set3DVDXAsis(gpu3DVDXAxis);


            _gpuService?.SetXAxis(gpu3DXAxis);
        }

        public IEnumerable<ICartesianAxis> Gpu3DXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DYAxes { get; set; }


        public IEnumerable<ICartesianAxis> Gpu3DCopyXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DCopyYAxes { get; set; }


        public IEnumerable<ICartesianAxis> Gpu3DVDXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVDYAxes { get; set; }



        public IEnumerable<ICartesianAxis> Gpu3DVEXAxes { get; set; }
        public IEnumerable<ICartesianAxis> Gpu3DVEYAxes { get; set; }




        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }



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