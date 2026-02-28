using FileCatalog.Services.Core;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileCatalog.Services.Settings;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext { }

public class AppSettings
{
    public string Language { get; set; } = "en";
    public bool AutoOpenLastCatalog { get; set; } = false;
    public string? LastCatalogPath { get; set; } = null;

    public bool AutoCalculateFolderSizes { get; set; } = false;
    public bool ReadId3Tags { get; set; } = true;

    public bool ShowPathColumn { get; set; } = true;
    public bool ShowExtensionColumn { get; set; } = true;
    public bool ShowSizeColumn { get; set; } = true;
    public bool ShowModifiedDateColumn { get; set; } = true;

    // AUDIT: Odstraněn mrtvý kód (Dictionary ColumnOrders)
}

public class SettingsManager
{
    private readonly string _settingsFilePath;
    public AppSettings Settings { get; private set; }

    public SettingsManager(PathProvider pathProvider)
    {
        _settingsFilePath = pathProvider.GetSettingsPath();
        Settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
            catch { }
        }
        return new AppSettings();
    }

    public void SaveSettings()
    {
        string json = JsonSerializer.Serialize(Settings, SettingsJsonContext.Default.AppSettings);
        File.WriteAllText(_settingsFilePath, json);
    }
}