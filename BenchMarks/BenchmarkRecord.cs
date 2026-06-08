
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace CoreStrike.Models;

[Table("benchmark_history")]
public class BenchmarkRecord : BaseModel
{
    [PrimaryKey("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }

    [Column("user_name")]
    public string UserName { get; set; }

    [Column("pc_name")]
    public string PcName { get; set; }

    [Column("pc_model")]
    public string PcModel { get; set; }

    [Column("cpu_score")]
    public int CpuScore { get; set; }

    [Column("memory_score")]
    public int MemoryScore { get; set; }

    [Column("disk_score")]
    public int DiskScore { get; set; }

    [Column("overall_score")]
    public int OverallScore { get; set; }

    [Column("grade")]
    public string Grade { get; set; }

    [Column("benchmark_date")]
    public DateTime BenchmarkDate { get; set; }

    [Column("country")]
    public string Country { get; set; }

    [Column("CpuInfo")]
    public string CpuInfo { get; set; }


    [Column("RamInfo")]
    public string RamInfo { get; set; }

    [Column("DiskInfo")]
    public string DiskInfo { get; set; }

    [Column("GpuInfo")]
    public string GpuInfo { get; set; }

    [Column("OsInfo")]
    public string OsInfo { get; set; }

    [Column("MoboInfo")]
    public string MoboInfo { get; set; }

}