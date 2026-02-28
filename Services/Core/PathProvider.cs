using System;
using System.IO;

namespace FileCatalog.Services.Core;

public class PathProvider
{
    public string AppDataDirectory { get; }
    public string TempDatabasePath { get; }

    public PathProvider()
    {
        // AppData pro trvalá uživatelská nastavení, logy, jazyky a výchozí katalogy
        AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileCatalog");
        if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);

        // OS Temp pro masivní pracovní databázi, o kterou se systém postará sám
        TempDatabasePath = Path.Combine(Path.GetTempPath(), "FileCatalog_temp.kat");
    }

    public string GetSettingsPath() => Path.Combine(AppDataDirectory, "settings.json");

    public string GetLangsDirectory()
    {
        string path = Path.Combine(AppDataDirectory, "Langs");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public string GetCatalogsDirectory()
    {
        string path = Path.Combine(AppDataDirectory, "Catalogs");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }
}