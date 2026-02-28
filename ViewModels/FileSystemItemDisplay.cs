using CommunityToolkit.Mvvm.ComponentModel;

namespace FileCatalog.ViewModels;

public partial class FileSystemItemDisplay : ObservableObject
{
    public bool IsFolder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;

    [ObservableProperty]
    private long? _sizeBytes;

    public long? ModifiedTicks { get; set; }
    public string Path { get; set; } = string.Empty;
    public int FolderId { get; set; }

    // NEW: Audio metadata
    public string? Title { get; set; }
    public string? Artist { get; set; }
}