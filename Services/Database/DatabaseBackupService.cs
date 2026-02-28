using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace FileCatalog.Services.Database;

public class DatabaseBackupService
{
    /// <summary>
    /// Načte a dekomprimuje uživatelský katalog do dočasného pracovního prostoru.
    /// Zajišťuje zpětnou kompatibilitu se starými (nekomprimovanými) katalogy.
    /// </summary>
    public async Task LoadCatalogFromFileAsync(string sourceCompressedPath, string destinationTempPath)
    {
        await using var sourceStream = new FileStream(sourceCompressedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        bool isCompressed = IsGZipHeader(sourceStream);
        sourceStream.Position = 0; // Reset pozice po přečtení hlavičky

        await using var destinationStream = new FileStream(destinationTempPath, FileMode.Create, FileAccess.Write, FileShare.None);

        if (isCompressed)
        {
            await using var decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress);
            await decompressionStream.CopyToAsync(destinationStream);
        }
        else
        {
            await sourceStream.CopyToAsync(destinationStream);
        }
    }

    /// <summary>
    /// Vyčistí dočasnou databázi (VACUUM) a uloží ji zkomprimovanou k uživateli.
    /// </summary>
    public async Task SaveCatalogToFileAsync(string sourceTempPath, string destinationFilePath)
    {
        // Architektonický best-practice: Před kompresí odstraníme fragmentaci databáze
        using (var connection = new SqliteConnection($"Data Source={sourceTempPath}"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "VACUUM;";
            await command.ExecuteNonQueryAsync();
        }

        // Extrémně rychlá GZip komprese proudu dat
        await using var sourceStream = new FileStream(sourceTempPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var compressionStream = new GZipStream(destinationStream, CompressionLevel.Optimal);

        await sourceStream.CopyToAsync(compressionStream);
    }

    private bool IsGZipHeader(Stream stream)
    {
        if (stream.Length < 2) return false;
        int byte1 = stream.ReadByte();
        int byte2 = stream.ReadByte();
        return byte1 == 0x1F && byte2 == 0x8B; // Magická hlavička GZip souborů
    }
}