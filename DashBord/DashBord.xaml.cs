using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            Unloaded += (s, e) =>
            {
                _cpuService?.Cleanup();
            };
        }

        private void InitializeChart()
        {
            XAxes = new[]
            {
                new Axis
                {
                    IsVisible = false,
                    SeparatorsAtCenter = false,
                    ShowSeparatorLines = false,
                }
            };

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
                    Fill = new SolidColorPaint(new SKColor(100, 180, 255, 128)),
                    Stroke = new SolidColorPaint(new SKColor(100, 180, 255, 255)) { StrokeThickness = 2 },
                    GeometrySize = 0,
                    LineSmoothness = 0.5,
                }
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

  
    }
}