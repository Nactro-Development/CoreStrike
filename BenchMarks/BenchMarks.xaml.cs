using CoreStrike.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace CoreStrike.BenchMarks;

public sealed partial class BenchMarks : Page
{
    private readonly BenchmarkService _benchmarkService;
    private readonly SupabaseService _supabase;

    private ObservableCollection<BenchmarkHistoryItem>
        _history = new();

    public BenchMarks()
    {
        InitializeComponent();

        _benchmarkService =
            new BenchmarkService();

        _supabase =
            new SupabaseService();

        HistoryListView.ItemsSource =
            _history;

        Loaded += BenchMarks_Loaded;
    }

    private async void BenchMarks_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await _supabase.InitializeAsync();

            var history =
                await _supabase.GetMyBenchmarksAsync();

            _history.Clear();


            if (history.Count > 0)
            {
                var latest = history[0];

                CpuScoreText.Text =
                    latest.CpuScore.ToString("N0");

                MemoryScoreText.Text =
                    latest.MemoryScore.ToString("N0");

                DiskScoreText.Text =
                    latest.DiskScore.ToString("N0");

                OverallScoreText.Text =
                    latest.OverallScore.ToString("N0");

                GradeText.Text =
                    $"Grade: {latest.Grade}";

                CpuDetailsText.Text = "";
                MemoryDetailsText.Text = "";
                DiskDetailsText.Text = "";
            }


            foreach (var item in history)
            {
                _history.Add(
                    new BenchmarkHistoryItem
                    {
                        Date =
                            item.BenchmarkDate
                            .ToLocalTime()
                            .ToString("yyyy-MM-dd HH:mm"),

                        CpuScore =
                            item.CpuScore,

                        MemoryScore =
                            item.MemoryScore,

                        DiskScore =
                            item.DiskScore,

                        OverallScore =
                            item.OverallScore,

                        Grade =
                            item.Grade
                    });
            }
        }
        catch
        {
        }
    }

    private async void RunBenchmarkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RunBenchmarkButton.IsEnabled = false;

        BenchmarkProgress.Visibility =
            Visibility.Visible;

        OverallScoreText.Text =
            "Running...";

        GradeText.Text = "";

        try
        {
            var result =
                await _benchmarkService
                .RunFullBenchmarkAsync();

            CpuScoreText.Text =
                result.CpuScore.ToString("N0");

            MemoryScoreText.Text =
                result.MemoryScore.ToString("N0");

            DiskScoreText.Text =
                result.DiskScore.ToString("N0");

            OverallScoreText.Text =
                result.OverallScore.ToString("N0");

            GradeText.Text =
                $"Grade: {result.Grade}";

            CpuDetailsText.Text =
                $"CPU Time: {result.CpuSeconds:F2}s";

            MemoryDetailsText.Text =
                $"Memory Speed: {result.MemoryMBps:F0} MB/s";

            DiskDetailsText.Text =
                $"Disk Speed: {result.DiskMBps:F0} MB/s";

            await _supabase
                .SaveBenchmarkAsync(result);

            _history.Insert(0,
                new BenchmarkHistoryItem
                {
                    Date =
                        DateTime.Now
                        .ToString("yyyy-MM-dd HH:mm"),

                    CpuScore =
                        result.CpuScore,

                    MemoryScore =
                        result.MemoryScore,

                    DiskScore =
                        result.DiskScore,

                    OverallScore =
                        result.OverallScore,

                    Grade =
                        result.Grade
                });
        }
        catch (Exception ex)
        {
            ContentDialog dialog =
                new ContentDialog
                {
                    Title = "Error",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };

            await dialog.ShowAsync();
        }

        BenchmarkProgress.Visibility =
            Visibility.Collapsed;

        RunBenchmarkButton.IsEnabled =
            true;
    }


    public class BenchmarkHistoryItem
    {
        public string Date { get; set; }

        public int CpuScore { get; set; }

        public int MemoryScore { get; set; }

        public int DiskScore { get; set; }

        public int OverallScore { get; set; }

        public string Grade { get; set; }
    }


}