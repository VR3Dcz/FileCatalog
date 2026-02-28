using Dapper;
using FileCatalog.Models;
using FileCatalog.ViewModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileCatalog.Services.Database;

public class CatalogRepository
{
    private string _connectionString;

    public CatalogRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public void ChangeDatabase(string newDbPath)
    {
        _connectionString = $"Data Source={newDbPath}";
    }

    public async Task InitializeDatabaseSchemaAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var exists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FileItems_fts'");
        if (exists == 0)
        {
            await connection.ExecuteAsync("CREATE VIRTUAL TABLE FileItems_fts USING fts5(Name, content='FileItems', content_rowid='rowid')");

            await connection.ExecuteAsync(@"
                CREATE TRIGGER t_FileItems_ai AFTER INSERT ON FileItems BEGIN
                    INSERT INTO FileItems_fts(rowid, Name) VALUES (new.rowid, new.Name);
                END;
                CREATE TRIGGER t_FileItems_ad AFTER DELETE ON FileItems BEGIN
                    INSERT INTO FileItems_fts(FileItems_fts, rowid, Name) VALUES('delete', old.rowid, old.Name);
                END;
                CREATE TRIGGER t_FileItems_au AFTER UPDATE ON FileItems BEGIN
                    INSERT INTO FileItems_fts(FileItems_fts, rowid, Name) VALUES('delete', old.rowid, old.Name);
                    INSERT INTO FileItems_fts(rowid, Name) VALUES (new.rowid, new.Name);
                END;
            ");
            await connection.ExecuteAsync("INSERT INTO FileItems_fts(FileItems_fts) VALUES('rebuild')");
        }
    }

    public async Task<IEnumerable<FileSystemItemDisplay>> SearchFilesAsync(string query, bool useRegex)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        IEnumerable<FileSystemItemDisplay> result;

        if (useRegex)
        {
            connection.CreateFunction("REGEXP", (string pattern, string? input) =>
            {
                if (input == null) return false;
                return System.Text.RegularExpressions.Regex.IsMatch(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            });

            string sql = @"
                SELECT 0 AS IsFolder, fi.FolderId, fi.Name, fi.Extension, fi.SizeBytes, fi.ModifiedTicks, fi.Artist, fi.Title, d.Name || fol.RelativePath as Path
                FROM FileItems fi
                INNER JOIN Folders fol ON fi.FolderId = fol.Id
                INNER JOIN Drives d ON fol.DriveId = d.Id
                WHERE fi.Name REGEXP @Query LIMIT 1000";
            result = await connection.QueryAsync<FileSystemItemDisplay>(sql, new { Query = query });
        }
        else
        {
            string sql = @"
                SELECT 0 AS IsFolder, fi.FolderId, fi.Name, fi.Extension, fi.SizeBytes, fi.ModifiedTicks, fi.Artist, fi.Title, d.Name || fol.RelativePath as Path
                FROM FileItems_fts fts
                JOIN FileItems fi ON fts.rowid = fi.rowid
                INNER JOIN Folders fol ON fi.FolderId = fol.Id
                INNER JOIN Drives d ON fol.DriveId = d.Id
                WHERE FileItems_fts MATCH @Query LIMIT 1000";

            string ftsQuery = $"\"{query.Replace("\"", "\"\"")}\"*";
            result = await connection.QueryAsync<FileSystemItemDisplay>(sql, new { Query = ftsQuery });
        }

        return result.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    public async Task<IEnumerable<Drive>> GetDrivesAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<Drive>("SELECT * FROM Drives ORDER BY SortOrder ASC");
    }

    public async Task<int> GetOrCreateDriveAsync(string name, string identifier)
    {
        using var connection = new SqliteConnection(_connectionString);
        var driveId = await connection.ExecuteScalarAsync<int?>("SELECT Id FROM Drives WHERE Identifier COLLATE NOCASE = @Identifier", new { Identifier = identifier });
        long nowTicks = DateTime.UtcNow.Ticks;

        if (driveId.HasValue)
        {
            await connection.ExecuteAsync("UPDATE Drives SET LastScannedTicks = @Ticks WHERE Id = @Id", new { Ticks = nowTicks, Id = driveId.Value });
            return driveId.Value;
        }

        var maxSortOrder = await connection.ExecuteScalarAsync<int?>("SELECT MAX(SortOrder) FROM Drives") ?? -1;
        return await connection.ExecuteScalarAsync<int>(
            "INSERT INTO Drives (Identifier, Name, SortOrder, LastScannedTicks) VALUES (@Identifier, @Name, @SortOrder, @Ticks); SELECT last_insert_rowid();",
            new { Identifier = identifier, Name = name, SortOrder = maxSortOrder + 1, Ticks = nowTicks });
    }

    public async Task UpdateDriveSortOrderAsync(int driveId, int sortOrder)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("UPDATE Drives SET SortOrder = @SortOrder WHERE Id = @Id", new { SortOrder = sortOrder, Id = driveId });
    }

    public async Task UpdateDriveNameAsync(int driveId, string newName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("UPDATE Drives SET Name = @Name WHERE Id = @Id", new { Name = newName, Id = driveId });
    }

    public async Task DeleteDriveAsync(int driveId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM Drives WHERE Id = @Id", new { Id = driveId });
    }

    public async Task ClearDriveContentsAsync(int driveId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM Folders WHERE DriveId = @DriveId", new { DriveId = driveId });
    }

    public async Task<IEnumerable<Folder>> GetSubFoldersAsync(int driveId, int? parentFolderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        IEnumerable<Folder> result;
        if (parentFolderId.HasValue)
            result = await connection.QueryAsync<Folder>("SELECT * FROM Folders WHERE ParentId = @ParentId", new { ParentId = parentFolderId.Value });
        else
            result = await connection.QueryAsync<Folder>("SELECT * FROM Folders WHERE DriveId = @DriveId AND ParentId IS NULL", new { DriveId = driveId });

        return result.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    public async Task UpdateFolderNameAsync(int folderId, string newName)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("UPDATE Folders SET Name = @Name WHERE Id = @Id", new { Name = newName, Id = folderId });
    }

    public async Task<IEnumerable<FileItem>> GetFilesAsync(int folderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var result = await connection.QueryAsync<FileItem>("SELECT * FROM FileItems WHERE FolderId = @FolderId", new { FolderId = folderId });

        return result.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    public async Task<IEnumerable<int>> GetFolderPathIdsAsync(int folderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var pathIds = new List<int>();
        int? currentId = folderId;

        while (currentId.HasValue)
        {
            pathIds.Insert(0, currentId.Value);
            currentId = await connection.ExecuteScalarAsync<int?>("SELECT ParentId FROM Folders WHERE Id = @Id", new { Id = currentId.Value });
        }
        return pathIds;
    }

    public async Task<long> GetFolderTotalSizeAsync(int folderId)
    {
        using var connection = new SqliteConnection(_connectionString);
        string sql = @"
            WITH RECURSIVE FolderHierarchy AS (
                SELECT Id FROM Folders WHERE Id = @RootFolderId
                UNION ALL
                SELECT f.Id FROM Folders f
                INNER JOIN FolderHierarchy fh ON f.ParentId = fh.Id
            )
            SELECT SUM(fi.SizeBytes) 
            FROM FileItems fi 
            INNER JOIN FolderHierarchy fh ON fi.FolderId = fh.Id;";

        return await connection.ExecuteScalarAsync<long?>(sql, new { RootFolderId = folderId }) ?? 0;
    }

    public CatalogBulkInserter CreateBulkInserter() => new CatalogBulkInserter(_connectionString);
}

public class CatalogBulkInserter : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly SqliteCommand _folderCmd, _fileCmd;
    private readonly SqliteParameter _fDriveId, _fParentId, _fName, _fPath;
    private readonly SqliteParameter _fiFolderId, _fiName, _fiExt, _fiSize, _fiTicks, _fiArtist, _fiTitle;

    public CatalogBulkInserter(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = WAL;";
            pragma.ExecuteNonQuery();
        }

        _transaction = _connection.BeginTransaction();

        _folderCmd = _connection.CreateCommand();
        _folderCmd.Transaction = _transaction;
        _folderCmd.CommandText = "INSERT INTO Folders (DriveId, ParentId, Name, RelativePath) VALUES (@DriveId, @ParentId, @Name, @RelativePath); SELECT last_insert_rowid();";

        _fDriveId = _folderCmd.Parameters.Add("@DriveId", SqliteType.Integer);
        _fParentId = _folderCmd.Parameters.Add("@ParentId", SqliteType.Integer);
        _fName = _folderCmd.Parameters.Add("@Name", SqliteType.Text);
        _fPath = _folderCmd.Parameters.Add("@RelativePath", SqliteType.Text);
        _folderCmd.Prepare();

        _fileCmd = _connection.CreateCommand();
        _fileCmd.Transaction = _transaction;
        _fileCmd.CommandText = "INSERT INTO FileItems (FolderId, Name, Extension, SizeBytes, ModifiedTicks, Artist, Title) VALUES (@FolderId, @Name, @Extension, @SizeBytes, @ModifiedTicks, @Artist, @Title)";

        _fiFolderId = _fileCmd.Parameters.Add("@FolderId", SqliteType.Integer);
        _fiName = _fileCmd.Parameters.Add("@Name", SqliteType.Text);
        _fiExt = _fileCmd.Parameters.Add("@Extension", SqliteType.Text);
        _fiSize = _fileCmd.Parameters.Add("@SizeBytes", SqliteType.Integer);
        _fiTicks = _fileCmd.Parameters.Add("@ModifiedTicks", SqliteType.Integer);
        _fiArtist = _fileCmd.Parameters.Add("@Artist", SqliteType.Text);
        _fiTitle = _fileCmd.Parameters.Add("@Title", SqliteType.Text);
        _fileCmd.Prepare();
    }

    public int InsertFolder(int driveId, int? parentId, string name, string path)
    {
        _fDriveId.Value = driveId; _fParentId.Value = parentId.HasValue ? parentId.Value : DBNull.Value;
        _fName.Value = name; _fPath.Value = path;
        return Convert.ToInt32(_folderCmd.ExecuteScalar());
    }

    public void InsertFile(int folderId, FileItem file)
    {
        _fiFolderId.Value = folderId; _fiName.Value = file.Name ?? (object)DBNull.Value;
        _fiExt.Value = file.Extension ?? (object)DBNull.Value; _fiSize.Value = file.SizeBytes;
        _fiTicks.Value = file.ModifiedTicks; _fiArtist.Value = file.Artist ?? (object)DBNull.Value;
        _fiTitle.Value = file.Title ?? (object)DBNull.Value;
        _fileCmd.ExecuteNonQuery();
    }

    public void Commit() => _transaction.Commit();

    public void Dispose()
    {
        _folderCmd.Dispose(); _fileCmd.Dispose(); _transaction.Dispose(); _connection.Dispose();
    }
}