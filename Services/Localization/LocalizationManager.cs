using FileCatalog.Services.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileCatalog.Services.Localization;

public class LanguageInfo
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LangJsonContext : JsonSerializerContext { }

public class LocalizationManager
{
    private readonly string _langsDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public LocalizationManager(PathProvider pathProvider)
    {
        _langsDir = pathProvider.GetLangsDirectory();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = LangJsonContext.Default
        };
        EnsureDefaultLanguagesExist();
    }

    private void EnsureDefaultLanguagesExist()
    {
        if (!Directory.Exists(_langsDir)) Directory.CreateDirectory(_langsDir);
        CreateIfMissing("en", "English", GetEnglishDefaults());
        CreateIfMissing("cs", "Čeština", GetCzechDefaults());
    }

    private void CreateIfMissing(string code, string name, Dictionary<string, string> translations)
    {
        string path = Path.Combine(_langsDir, $"{code}.json");
        if (!File.Exists(path))
        {
            translations["_LangName"] = name;
            var json = JsonSerializer.Serialize(translations, _jsonOptions);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }
    }

    public List<LanguageInfo> GetAvailableLanguages()
    {
        var list = new List<LanguageInfo>();
        if (Directory.Exists(_langsDir))
        {
            foreach (var file in Directory.GetFiles(_langsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions);
                    if (dict != null && dict.TryGetValue("_LangName", out string? name))
                    {
                        list.Add(new LanguageInfo { Code = Path.GetFileNameWithoutExtension(file), Name = name });
                    }
                }
                catch { }
            }
        }
        return list.OrderBy(x => x.Code).ToList();
    }

    public Dictionary<string, string> LoadLanguage(string code)
    {
        string path = Path.Combine(_langsDir, $"{code}.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions) ?? GetEnglishDefaults();
            }
            catch { }
        }
        return GetEnglishDefaults();
    }

    private Dictionary<string, string> GetEnglishDefaults() => new()
    {
        ["MenuFile"] = "_File",
        ["MenuNew"] = "_New Catalog...",
        ["MenuOpen"] = "_Open Catalog...",
        ["MenuSave"] = "_Save",
        ["MenuSaveAs"] = "Save _As...",
        ["MenuQuit"] = "_Quit",
        ["MenuView"] = "_View",
        ["MenuColumns"] = "Columns",
        ["MenuSettings"] = "_Settings",
        ["MenuPreferences"] = "Preferences...",
        ["BtnScan"] = "Scan New Drive",
        ["BtnSearch"] = "Search",
        ["LblSettings"] = "Application Settings",
        ["ColName"] = "Name",
        ["ColArtist"] = "Artist",
        ["ColTitle"] = "Title",
        ["ColPath"] = "Path",
        ["ColExt"] = "Ext",
        ["ColSize"] = "Size",
        ["ColDate"] = "Modified Date",
        ["SetLang"] = "Language:",
        ["SetAutoOpen"] = "Automatically open the last catalog on startup",
        ["SetAutoCalc"] = "Auto-calculate folder sizes when opening",
        ["SetId3"] = "Read ID3 tags (Artist, Title) when scanning audio files",
        ["BtnSaveClose"] = "Save and Close"
    };

    private Dictionary<string, string> GetCzechDefaults() => new()
    {
        ["MenuFile"] = "_Soubor",
        ["MenuNew"] = "_Nový katalog...",
        ["MenuOpen"] = "_Otevřít katalog...",
        ["MenuSave"] = "_Uložit",
        ["MenuSaveAs"] = "Uložit j_ako...",
        ["MenuQuit"] = "_Ukončit",
        ["MenuView"] = "_Zobrazit",
        ["MenuColumns"] = "Sloupce",
        ["MenuSettings"] = "_Nastavení",
        ["MenuPreferences"] = "Předvolby...",
        ["BtnScan"] = "Skenovat nový disk",
        ["BtnSearch"] = "Hledat",
        ["LblSettings"] = "Nastavení aplikace",
        ["ColName"] = "Název",
        ["ColArtist"] = "Interpret",
        ["ColTitle"] = "Skladba",
        ["ColPath"] = "Cesta",
        ["ColExt"] = "Přípona",
        ["ColSize"] = "Velikost",
        ["ColDate"] = "Datum úpravy",
        ["SetLang"] = "Jazyk:",
        ["SetAutoOpen"] = "Automaticky otevřít poslední katalog po spuštění",
        ["SetAutoCalc"] = "Automaticky spočítat velikost složek po otevření",
        ["SetId3"] = "Číst ID3 tagy (Interpret, Skladba) při skenování audia",
        ["BtnSaveClose"] = "Uložit a zavřít"
    };
}