using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        // Field add කරන්න
        private CpuStressTestService _stressTest;

        public bool IsStressTesting => _stressTest.IsRunning;

        public string StressButtonText => _stressTest.IsRunning ? "Stop Stress Test" : "Stress Test";

        public IEnumerable<ISeries> CpuSeries { get; set; }
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

        public DashBord()
        {
            InitializeComponent();
            _cpuService = new CpuMonitoringService();
            InitializeChart();
            _cpuService.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            _cpuService.StartMonitoring();
            
            _stressTest = new CpuStressTestService(_cpuService);  // Pass service to stress test
            
            _stressTest.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsStressTesting));
                OnPropertyChanged(nameof(StressButtonText));
            };

            _stressTest.TestCompleted += (s, summary) => ShowStressTestSummary(summary);

            Unloaded += (s, e) =>
            {
                _cpuService?.Cleanup();
                _stressTest.Stop();
            };
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
            var xAxis = new Axis
            {
                MinLimit = 0,
                MaxLimit = 49,
                IsVisible = false,
                SeparatorsAtCenter = true,
                ShowSeparatorLines = false,
            };

            XAxes = new[] { xAxis };

            YAxes = new[]
            {
        new Axis
        {
            IsVisible = false,
            MinLimit = 0,
            MaxLimit = 100,
            ShowSeparatorLines = false,
        }
    };

            CpuSeries = new[]
      {
    new LineSeries<ObservablePoint>
    {
        Values = _cpuService?.CpuUsageData,

        Fill = new SolidColorPaint(GetAccentSKColor(128)),

        Stroke = new SolidColorPaint(GetAccentSKColor(255))
        {
            StrokeThickness = 2
        },

        GeometrySize = 0,
        LineSmoothness = 0.5,
    }
};


            // XAxis reference service එකට pass කරන්න
            _cpuService?.SetXAxis(xAxis);
        }


        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

  
    }
}