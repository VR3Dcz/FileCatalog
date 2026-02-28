using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FileCatalog.Models;
using FileCatalog.Services.Database;

namespace FileCatalog.ViewModels;

public partial class FolderNodeViewModel : ObservableObject
{
    private readonly CatalogRepository _repository;
    private bool _isLoaded;

    public Folder Folder { get; }

    /// <summary>
    /// Populated only if this folder is the root of a Drive.
    /// </summary>
    public Drive? Drive { get; set; }

    /// <summary>
    /// Checks if this node represents a root drive structure.
    /// </summary>
    public bool IsDriveRoot => Folder.ParentId == null;

    [ObservableProperty]
    private bool _isEditing;

    public string Name
    {
        get => Folder.Name;
        set
        {
            if (Folder.Name != value)
            {
                Folder.Name = value;
                OnPropertyChanged();

                // Save name change to database asynchronously
                _ = _repository.UpdateFolderNameAsync(Folder.Id, value);

                if (IsDriveRoot && Drive != null)
                {
                    Drive.Name = value;
                    _ = _repository.UpdateDriveNameAsync(Drive.Id, value);
                }
            }
        }
    }

    [ObservableProperty]
    private ObservableCollection<FolderNodeViewModel> _subFolders = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                _ = ExpandAsync();
            }
        }
    }

    public FolderNodeViewModel(Folder folder, CatalogRepository repository, bool addDummyChild = true)
    {
        Folder = folder;
        _repository = repository;
        if (addDummyChild)
        {
            SubFolders.Add(new FolderNodeViewModel(new Folder { Name = "Loading..." }, repository, false));
        }
    }

    public async Task ExpandAsync()
    {
        if (_isLoaded)
        {
            IsExpanded = true;
            return;
        }

        var children = await _repository.GetSubFoldersAsync(Folder.DriveId, Folder.Id);
        SubFolders.Clear();

        foreach (var child in children)
        {
            SubFolders.Add(new FolderNodeViewModel(child, _repository, true));
        }

        _isLoaded = true;
        IsExpanded = true;
    }
}