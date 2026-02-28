using Microsoft.Data.Sqlite;

namespace FileCatalog.Services.Database;

public class DatabaseInitializer
{
    public void Initialize(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Drives (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Identifier TEXT NOT NULL,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                LastScannedTicks INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DriveId INTEGER NOT NULL,
                ParentId INTEGER,
                Name TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                FOREIGN KEY (DriveId) REFERENCES Drives(Id) ON DELETE CASCADE,
                FOREIGN KEY (ParentId) REFERENCES Folders(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS FileItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Extension TEXT,
                SizeBytes INTEGER NOT NULL,
                ModifiedTicks INTEGER NOT NULL,
                Artist TEXT,
                Title TEXT,
                FOREIGN KEY (FolderId) REFERENCES Folders(Id) ON DELETE CASCADE
            );

            -- AUDIT: Klasický B-Tree index nad sloupcem Name byl smazán, 
            -- protože ho plně nahradil FTS5 invertovaný index.
            CREATE INDEX IF NOT EXISTS IX_Folders_DriveId ON Folders(DriveId);
            CREATE INDEX IF NOT EXISTS IX_Folders_ParentId ON Folders(ParentId);
            CREATE INDEX IF NOT EXISTS IX_FileItems_FolderId ON FileItems(FolderId);
        ";
        command.ExecuteNonQuery();
    }
}