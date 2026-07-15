using CoreStrike.Models;
using CoreStrike.Settings;
using System;
using System.IO;
using System.Text.Json;

namespace CoreStrike.Services;

public static class SettingsService
{
    private static readonly string Folder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoreStrike");

    private static readonly string FilePath =
        Path.Combine(Folder, "settings.json");

    public static SettingsModel Settings { get; private set; } = new();

    public static void Load()
    {
        Directory.CreateDirectory(Folder);

        if (!File.Exists(FilePath))
        {
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(FilePath);

            Settings = JsonSerializer.Deserialize<SettingsModel>(json)
                       ?? new SettingsModel();
        }
        catch
        {
            Settings = new SettingsModel();
            Save();
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Folder);

        var json = JsonSerializer.Serialize(
            Settings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(FilePath, json);
    }
}