using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FileCatalog.Services.Core;
using System;
using System.Threading.Tasks;

namespace FileCatalog.Services.UI;

public class DialogService : IDialogService
{
    private readonly Window _ownerWindow;
    private readonly PathProvider _pathProvider;
    private readonly FilePickerFileType _catalogFileType = new("Catalog Files") { Patterns = new[] { "*.kat" } };

    public DialogService(Window ownerWindow, PathProvider pathProvider)
    {
        _ownerWindow = ownerWindow;
        _pathProvider = pathProvider;
    }

    private async Task<IStorageFolder?> GetCatalogsFolderAsync()
    {
        string catalogsPath = _pathProvider.GetCatalogsDirectory();
        return await _ownerWindow.StorageProvider.TryGetFolderFromPathAsync(new Uri(catalogsPath));
    }

    private async Task<IStorageFolder?> GetSystemRootFolderAsync()
    {
        string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return await _ownerWindow.StorageProvider.TryGetFolderFromPathAsync(new Uri(rootPath));
    }

    public async Task<string?> ShowFolderPickerDialogAsync()
    {
        var startFolder = await GetSystemRootFolderAsync();
        var result = await _ownerWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder or Drive to Scan",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder
        });
        return result?.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> ShowOpenFileDialogAsync()
    {
        var startFolder = await GetCatalogsFolderAsync();
        var result = await _ownerWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Catalog",
            AllowMultiple = false,
            FileTypeFilter = new[] { _catalogFileType },
            SuggestedStartLocation = startFolder
        });
        return result?.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> ShowSaveFileDialogAsync()
    {
        var startFolder = await GetCatalogsFolderAsync();
        var result = await _ownerWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Catalog As",
            DefaultExtension = ".kat",
            FileTypeChoices = new[] { _catalogFileType },
            SuggestedStartLocation = startFolder
        });
        return result?.TryGetLocalPath();
    }
}