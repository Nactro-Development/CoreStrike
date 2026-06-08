using CoreStrike.Models;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace CoreStrike.Services;

public class SupabaseService
{
    private readonly Client _client;

    public SupabaseService()
    {
        _client = new Client(
            "https://mnckultfeooxthjdjhpu.supabase.co",
            "sb_publishable_iL8x8b4_ngA0TL1VZQGmRg_086xK0HL");
    }

    public async Task InitializeAsync()
    {
        await _client.InitializeAsync();
    }

    public async Task SaveBenchmarkAsync(
     FullBenchmarkResult result)
    {
        var user = UserProfile.Load();

        // Fix: BenchmarkService instance use කරන්න
        var benchmarkService = new BenchmarkService();
        var system = await benchmarkService.GetHardwareReportAsync();

        try
        {
            await _client
                .From<BenchmarkRecord>()
                .Insert(new BenchmarkRecord
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    PcName = user.PcName,
                    PcModel = user.PcModel,
                    Country = user.Country,

                    CpuScore = result.CpuScore,
                    MemoryScore = result.MemoryScore,
                    DiskScore = result.DiskScore,
                    OverallScore = result.OverallScore,
                    Grade = result.Grade,

                    BenchmarkDate = DateTime.UtcNow,

                    // Fix: proper string formatting
                    CpuInfo = $"{system.CpuName} {system.CpuGHz:F1}GHz " +
                              $"{system.CpuCores}C/{system.CpuThreads}T ({system.CpuArchitecture})",

                    // Hardware report fields add කරන්න
                    RamInfo = $"{system.RamGB}GB {system.RamType} {system.RamSpeedMHz}MHz",
                    DiskInfo = $"{system.DiskType} - {system.DiskModel} ({system.DiskSizeGB}GB)",
                    GpuInfo = $"{system.GpuName} ({system.GpuVramMB}MB)",
                    OsInfo = $"{system.OsName} {system.OsVersion}",
                    MoboInfo = system.MotherboardModel,
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.ToString());
            throw;
        }
    }


    public async Task<List<BenchmarkRecord>>
        GetMyBenchmarksAsync()
    {
        var user = UserProfile.Load();

        var response =
            await _client
            .From<BenchmarkRecord>()
            .Where(x => x.UserId == user.UserId)
            .Get();

        return response.Models
            .OrderByDescending(x => x.BenchmarkDate)
            .ToList();
    }

    public async Task<List<BenchmarkRecord>>
GetGlobalRankingsAsync()
    {
        var response =
            await _client
            .From<BenchmarkRecord>()
            .Get();

        return response.Models
            .OrderByDescending(x => x.OverallScore)
            .ToList();
    }


}