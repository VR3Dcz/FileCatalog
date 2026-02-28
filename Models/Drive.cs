using System;

namespace FileCatalog.Models;

/// <summary>
/// Represents a physical or network drive scanned by the application.
/// </summary>
public class Drive
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public long LastScannedTicks { get; set; }

    /// <summary>
    /// User-defined sorting order in the TreeView.
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime LastScanned => new DateTime(LastScannedTicks);
}