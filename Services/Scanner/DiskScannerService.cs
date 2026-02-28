using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileCatalog.Models;
using FileCatalog.Services.Core;
using FileCatalog.Services.Database;
using FileCatalog.Services.Settings;

namespace FileCatalog.Services.Scanner;

public class DiskScannerService
{
    private readonly CatalogRepository _repository;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;

    public DiskScannerService(string dbPath, AppSettings settings, AppLogger logger)
    {
        _repository = new CatalogRepository(dbPath);
        _settings = settings;
        _logger = logger;
    }

    public async Task ScanAndSaveAsync(string rootPath, int driveId)
    {
        await _logger.LogInfoAsync($"Zahájeno skenování cesty: {rootPath} (DriveID: {driveId})");
        var sw = Stopwatch.StartNew();

        // Inicializace FTS5 indexů a spouštěčů (Triggers) před začátkem masového zápisu
        await _repository.InitializeDatabaseSchemaAsync();

        var queue = new Queue<(string Path, int? ParentFolderId)>();
        queue.Enqueue((rootPath, null));

        var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
        using var inserter = _repository.CreateBulkInserter();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(current.Path);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Nelze přistoupit ke složce: {current.Path}", ex);
                continue;
            }

            int currentFolderId = inserter.InsertFolder(driveId, current.ParentFolderId, dirInfo.Name, current.Path);

            try
            {
                foreach (var subDir in dirInfo.EnumerateDirectories("*", enumOptions))
                    queue.Enqueue((subDir.FullName, currentFolderId));
            }
            catch (Exception ex) { await _logger.LogErrorAsync($"Odepřen přístup k podsložkám v: {current.Path}", ex); }

            List<FileInfo> fileInfos = new();
            try { fileInfos = dirInfo.EnumerateFiles("*", enumOptions).ToList(); }
            catch (Exception ex) { await _logger.LogErrorAsync($"Nelze načíst soubory v: {current.Path}", ex); continue; }

            if (fileInfos.Count == 0) continue;

            var audioFilesToProcess = new List<(FileInfo File, FileItem Item)>();

            foreach (var fi in fileInfos)
            {
                var item = new FileItem
                {
                    Name = fi.Name,
                    Extension = fi.Extension.ToLowerInvariant(),
                    SizeBytes = fi.Length,
                    ModifiedTicks = fi.LastWriteTimeUtc.Ticks
                };

                if (_settings.ReadId3Tags && IsAudioFile(item.Extension))
                    audioFilesToProcess.Add((fi, item));
                else
                    inserter.InsertFile(currentFolderId, item);
            }

            if (audioFilesToProcess.Count > 0)
            {
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                await Parallel.ForEachAsync(audioFilesToProcess, parallelOptions, async (pair, ct) =>
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            using var tfile = TagLib.File.Create(pair.File.FullName);
                            pair.Item.Artist = tfile.Tag.FirstPerformer ?? tfile.Tag.FirstAlbumArtist;
                            pair.Item.Title = tfile.Tag.Title;
                        }
                        catch { /* Poškozená metadata ignorujeme, soubor se uloží bez nich */ }
                    }, ct);
                });

                foreach (var pair in audioFilesToProcess) inserter.InsertFile(currentFolderId, pair.Item);
            }
        }

        inserter.Commit();
        sw.Stop();
        await _logger.LogInfoAsync($"Skenování úspěšně dokončeno. Zpracováno za: {sw.Elapsed.TotalSeconds:F2} s.");
    }

    private bool IsAudioFile(string ext) => ext is ".mp3" or ".flac" or ".wav" or ".m4a" or ".ogg" or ".wma";
}