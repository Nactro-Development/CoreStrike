using CoreStrike.Models;
using CoreStrike.Services;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Globalization;

namespace CoreStrike.Ranking;

public sealed partial class Ranking : Page
{
    private readonly SupabaseService _supabase =
        new();

    private ObservableCollection<RankingItem>
        _items = new();

    public Ranking()
    {
        InitializeComponent();

        RankingListView.ItemsSource = _items;

        Loaded += Ranking_Loaded;
    }

    private async void Ranking_Loaded(
        object sender,
        Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _supabase.InitializeAsync();

        var data =
            await _supabase.GetGlobalRankingsAsync();

        var currentUser =
            UserProfile.Load();

        int rank = 1;

        foreach (var item in data)
        {
            _items.Add(new RankingItem
            {
                Rank = rank,
                UserId = item.UserId,
                UserName = item.UserName,
                PcModel = item.PcModel,
                OverallScore = item.OverallScore,
                Grade = item.Grade,
                Country = item.Country,
                CpuInfo = item.CpuInfo,
                RamInfo = item.RamInfo,
                DiskInfo = item.DiskInfo,
                GpuInfo = item.GpuInfo,
                OsInfo = item.OsInfo,
                MoboInfo = item.MoboInfo

            });

            rank++;
        }

        if (_items.Count >= 3)
        {
            FirstPlaceText.Text =
                _items[0].UserName;

            FirstScoreText.Text =
                _items[0].OverallScore.ToString();

            FirstStorageText.Text =
                _items[0].DiskInfo;

            FirstMemoryText.Text =
                _items[0].RamInfo;

            FirstCPUText.Text =
                _items[0].CpuInfo;
            FirstGPUText.Text =
                _items[0].GpuInfo;
            FirstOSText.Text =
                _items[0].OsInfo;

            FirstMotherboardText.Text =
                _items[0].MoboInfo;

            SecondPlaceText.Text =
                _items[1].UserName;

            SecondScoreText.Text =
                _items[1].OverallScore.ToString();

            SecondStorageText.Text = _items[1].DiskInfo;
            SecondMemoryText.Text = _items[1].RamInfo;
            SecondCPUText.Text = _items[1].CpuInfo;
            SecondGPUText.Text = _items[1].GpuInfo;
            SecondOSText.Text = _items[1].OsInfo;
            SecondMotherboardText.Text = _items[1].MoboInfo;


            ThirdPlaceText.Text =
                _items[2].UserName;

            ThirdScoreText.Text =
                _items[2].OverallScore.ToString();


            ThirdStorageText.Text = _items[2].DiskInfo;
            ThirdMemoryText.Text = _items[2].RamInfo;
            ThirdCPUText.Text = _items[2].CpuInfo;
            ThirdGPUText.Text = _items[2].GpuInfo;
            ThirdOSText.Text = _items[2].OsInfo;
            ThirdMotherboardText.Text = _items[2].MoboInfo;
        }

        int myRank =
            _items
            .FirstOrDefault(x =>
                x.UserId ==
                currentUser.UserId)?.Rank ?? 0;

        MyRankText.Text =
            $"#{myRank} of {_items.Count}";

        RankProgressBar.Maximum =
            _items.Count;

        RankProgressBar.Value =
            _items.Count - myRank + 1;
    }



}
public class RankingItem
{
    public int Rank { get; set; }
    public string UserName { get; set; }
    public string PcModel { get; set; }
    public int OverallScore { get; set; }
    public string Grade { get; set; }
    public string UserId { get; set; }
    public string Country { get; set; }

    public string CpuInfo { get; set; }

    public string RamInfo { get; set; }

    public string DiskInfo { get; set; }

    public string GpuInfo { get; set; }
    public string OsInfo { get; set; }
    public string MoboInfo { get; set; }






    public string CountryFlagImage
    {
        get
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Country))
                    return "ms-appx:///Assets/Flags/1x1/unknown.svg";

                string countryCode;

                // Already ISO code (US, LK, IN...)
                if (Country.Length == 2)
                {
                    countryCode = Country.ToLowerInvariant();
                }
                else
                {
                    var region = CultureInfo
                        .GetCultures(CultureTypes.SpecificCultures)
                        .Select(c => new RegionInfo(c.Name))
                        .FirstOrDefault(r =>
                            r.EnglishName.Equals(
                                Country,
                                StringComparison.OrdinalIgnoreCase));

                    if (region == null)
                        return "ms-appx:///Assets/Flags/1x1/unknown.svg";

                    countryCode =
                        region.TwoLetterISORegionName.ToLowerInvariant();
                }

                return $"ms-appx:///Assets/Flags/1x1/{countryCode}.svg";
            }
            catch
            {
                return "ms-appx:///Assets/Flags/1x1/unknown.svg";
            }
        }
    }
}
