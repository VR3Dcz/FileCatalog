namespace FileCatalog.Models;

/// <summary>
/// Represents a directory inside a scanned drive.
/// </summary>
public class Folder
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Drive table.
    /// </summary>
    public int DriveId { get; set; }

    /// <summary>
    /// Foreign key to the parent folder. Null if it's the root directory.
    /// </summary>
    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Relative path from the drive root (e.g., "\Windows\System32").
    /// Storing this significantly speeds up search queries compared to recursive SQL joins.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;
}