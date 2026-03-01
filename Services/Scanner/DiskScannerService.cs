using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using FileCatalog.Services.Settings;
using FileCatalog.Services.Core;

namespace FileCatalog.Services.Scanner;

public class DiskScannerService
{
    private readonly string _databasePath;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;

    public DiskScannerService(string databasePath, AppSettings settings, AppLogger logger)
    {
        _databasePath = databasePath;
        _settings = settings;
        _logger = logger;
    }

    public async Task ScanAndSaveAsync(string rootPath, int driveId)
    {
        var rootDirectory = new DirectoryInfo(rootPath);
        if (!rootDirectory.Exists)
        {
            await _logger.LogErrorAsync($"Cannot scan directory. Path does not exist: {rootPath}", null);
            return;
        }

        var connectionString = $"Data Source={_databasePath};Pooling=True;";

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Iterative traversal using a Queue (Breadth-First Search) to prevent StackOverflowException
        var processingQueue = new Queue<(DirectoryInfo Directory, int? ParentFolderId)>();

        // Insert the root folder explicitly
        int rootFolderId = await InsertFolderAsync(connection, rootDirectory.Name, driveId, null, rootDirectory.FullName);
        processingQueue.Enqueue((rootDirectory, rootFolderId));

        // Explicit cast from DbTransaction to SqliteTransaction to satisfy strict parameter types
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            while (processingQueue.Count > 0)
            {
                var (currentDirectory, parentFolderId) = processingQueue.Dequeue();

                // 1. Process files safely
                await ProcessFilesInDirectoryAsync(connection, transaction, currentDirectory, parentFolderId);

                // 2. Process subdirectories safely
                foreach (var subDirectory in SafeDirectoryTraverser.EnumerateDirectoriesSafely(currentDirectory))
                {
                    int newFolderId = await InsertFolderAsync(connection, subDirectory.Name, driveId, parentFolderId, subDirectory.FullName, transaction);
                    processingQueue.Enqueue((subDirectory, newFolderId));
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await _logger.LogErrorAsync("Critical failure during disk scanning transaction.", ex);
            throw;
        }
    }

    private async Task ProcessFilesInDirectoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DirectoryInfo directory,
        int? folderId)
    {
        const string insertFileSql = @"
            INSERT INTO FileItems (Name, Extension, SizeBytes, ModifiedTicks, FolderId, Title, Artist) 
            VALUES (@Name, @Extension, @SizeBytes, @ModifiedTicks, @FolderId, @Title, @Artist);";

        await using var command = new SqliteCommand(insertFileSql, connection, transaction);

        var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
        var extParam = command.Parameters.Add("@Extension", SqliteType.Text);
        var sizeParam = command.Parameters.Add("@SizeBytes", SqliteType.Integer);
        var modifiedParam = command.Parameters.Add("@ModifiedTicks", SqliteType.Integer);
        var folderIdParam = command.Parameters.Add("@FolderId", SqliteType.Integer);
        var titleParam = command.Parameters.Add("@Title", SqliteType.Text);
        var artistParam = command.Parameters.Add("@Artist", SqliteType.Text);

        foreach (var file in SafeDirectoryTraverser.EnumerateFilesSafely(directory))
        {
            nameParam.Value = file.Name;
            extParam.Value = file.Extension.TrimStart('.').ToLowerInvariant();
            sizeParam.Value = file.Length;
            modifiedParam.Value = file.LastWriteTimeUtc.Ticks;
            folderIdParam.Value = folderId ?? (object)DBNull.Value;

            // Audio metadata extraction logic mocked as null for maximum throughput
            titleParam.Value = DBNull.Value;
            artistParam.Value = DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<int> InsertFolderAsync(
        SqliteConnection connection,
        string name,
        int driveId,
        int? parentId,
        string fullPath,
        SqliteTransaction? transaction = null)
    {
        const string insertFolderSql = @"
            INSERT INTO Folders (Name, DriveId, ParentId, RelativePath) 
            VALUES (@Name, @DriveId, @ParentId, @RelativePath);
            SELECT last_insert_rowid();";

        await using var command = new SqliteCommand(insertFolderSql, connection, transaction);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@DriveId", driveId);
        command.Parameters.AddWithValue("@ParentId", parentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RelativePath", fullPath);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}