namespace FileCatalog.Models;

public class FileItem
{
    public int Id { get; set; }
    public int FolderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long ModifiedTicks { get; set; }
    public string? Hash { get; set; }

    // NEW: Audio metadata
    public string? Title { get; set; }
    public string? Artist { get; set; }
}