using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCatalog.Models;
using FileCatalog.Services.Core;
using FileCatalog.Services.Database;
using FileCatalog.Services.Localization;
using FileCatalog.Services.Scanner;
using FileCatalog.Services.Settings;
using FileCatalog.Services.UI;
using FileCatalog.Utils;

namespace FileCatalog.ViewModels;

public enum PendingAction { None, Exit, New, Open }

public partial class MainViewModel : ObservableObject
{
    private readonly CatalogRepository _repository;
    private readonly IDialogService? _dialogService;
    private readonly SettingsManager _settingsManager;
    private readonly LocalizationManager _locManager;
    private readonly PathProvider _pathProvider;
    private readonly DatabaseBackupService _backupService;
    private readonly AppLogger _logger;

    private readonly string _tempFilePath;
    private string? _currentFilePath;
    private Avalonia.Controls.ResourceDictionary? _currentLangDict;

    public Action? RequestApplicationClose;
    private PendingAction _pendingAction = PendingAction.None;

    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private string _windowTitle = "File Catalog - Untitled";
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isUnsavedWarningOpen;

    [ObservableProperty] private bool _isStatusHistoryOpen;
    [ObservableProperty] private ObservableCollection<string> _statusHistory = new();

    public AppSettings Settings => _settingsManager.Settings;

    [ObservableProperty] private ObservableCollection<LanguageInfo> _availableLanguages = new();

    public string Language
    {
        get => Settings.Language;
        set
        {
            if (!string.IsNullOrEmpty(value) && Settings.Language != value)
            {
                Settings.Language = value;
                OnPropertyChanged();
                SaveSettings();
                ApplyLanguage(value);
            }
        }
    }

    public bool AutoOpenLastCatalog { get => Settings.AutoOpenLastCatalog; set { if (Settings.AutoOpenLastCatalog != value) { Settings.AutoOpenLastCatalog = value; OnPropertyChanged(); SaveSettings(); } } }

    public bool ShowPathColumn { get => Settings.ShowPathColumn; set { if (Settings.ShowPathColumn != value) { Settings.ShowPathColumn = value; OnPropertyChanged(); SaveSettings(); BuildColumns(); } } }
    public bool ShowExtensionColumn { get => Settings.ShowExtensionColumn; set { if (Settings.ShowExtensionColumn != value) { Settings.ShowExtensionColumn = value; OnPropertyChanged(); SaveSettings(); BuildColumns(); } } }
    public bool ShowSizeColumn { get => Settings.ShowSizeColumn; set { if (Settings.ShowSizeColumn != value) { Settings.ShowSizeColumn = value; OnPropertyChanged(); SaveSettings(); BuildColumns(); } } }
    public bool ShowModifiedDateColumn { get => Settings.ShowModifiedDateColumn; set { if (Settings.ShowModifiedDateColumn != value) { Settings.ShowModifiedDateColumn = value; OnPropertyChanged(); SaveSettings(); BuildColumns(); } } }

    private FolderNodeViewModel? _lastSelectedFolder;
    private bool _isNavigating;

    [ObservableProperty] private ObservableCollection<FolderNodeViewModel> _rootFolders = new();

    public AvaloniaList<FileSystemItemDisplay> Items { get; } = new();
    public FlatTreeDataGridSource<FileSystemItemDisplay> FilesSource { get; }

    [ObservableProperty] private FolderNodeViewModel? _selectedFolder;
    [ObservableProperty] private FileSystemItemDisplay? _selectedItem;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ready";

    [ObservableProperty] private bool _hasAudioMetadata;

    partial void OnStatusMessageChanged(string value)
    {
        var timestampedMsg = $"[{DateTime.Now:HH:mm:ss}] {value}";
        Dispatcher.UIThread.Post(() =>
        {
            StatusHistory.Insert(0, timestampedMsg);
        });
    }

    partial void OnHasAudioMetadataChanged(bool value)
    {
        BuildColumns();
    }

    public MainViewModel(
        CatalogRepository repository,
        IDialogService dialogService,
        SettingsManager settingsManager,
        LocalizationManager locManager,
        PathProvider pathProvider,
        DatabaseBackupService backupService,
        AppLogger logger)
    {
        _repository = repository;
        _dialogService = dialogService;
        _settingsManager = settingsManager;
        _locManager = locManager;
        _pathProvider = pathProvider;
        _backupService = backupService;
        _logger = logger;

        _tempFilePath = _pathProvider.TempDatabasePath;

        FilesSource = new FlatTreeDataGridSource<FileSystemItemDisplay>(Items);
        var selectionModel = new TreeDataGridRowSelectionModel<FileSystemItemDisplay>(FilesSource) { SingleSelect = true };
        selectionModel.SelectionChanged += OnSelectionChanged;
        FilesSource.Selection = selectionModel;

        AvailableLanguages = new ObservableCollection<LanguageInfo>(_locManager.GetAvailableLanguages());
        ApplyLanguage(Settings.Language);

        StatusHistory.Add($"[{DateTime.Now:HH:mm:ss}] Ready");
    }

    private void OnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileSystemItemDisplay> e)
    {
        if (e.SelectedItems.Count > 0) SelectedItem = e.SelectedItems[0];
    }

#pragma warning disable CS8618
    [Obsolete("Only for the XAML designer. Do not use in production.", true)]
    public MainViewModel() { }
#pragma warning restore CS8618

    public void SaveSettings() => _settingsManager.SaveSettings();

    public async Task InitializeStartupAsync()
    {
        bool autoOpened = false;

        if (Settings.AutoOpenLastCatalog && !string.IsNullOrEmpty(Settings.LastCatalogPath) && File.Exists(Settings.LastCatalogPath))
        {
            IsBusy = true; StatusMessage = "Auto-opening last catalog...";
            try
            {
                // Nový kompresní engine
                await _backupService.LoadCatalogFromFileAsync(Settings.LastCatalogPath, _tempFilePath);
                _currentFilePath = Settings.LastCatalogPath;
                _repository.ChangeDatabase(_tempFilePath);
                await LoadInitialDataAsync();
                UpdateTitle(); StatusMessage = "Catalog opened successfully.";
                autoOpened = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error auto-opening catalog: {ex.Message}";
                _logger.LogErrorAsync("Auto-open failed", ex).SafeFireAndForget();
            }
            finally { IsBusy = false; }
        }

        if (!autoOpened)
        {
            try
            {
                await Task.Run(() =>
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);

                    new DatabaseInitializer().Initialize(_tempFilePath);
                    _repository.ChangeDatabase(_tempFilePath);
                });

                await LoadInitialDataAsync();
                UpdateTitle();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Startup initialization error: {ex.Message}";
                _logger.LogErrorAsync("Failed to initialize clean temp database", ex).SafeFireAndForget();
            }
        }
    }

    private void ApplyLanguage(string langCode)
    {
        var translations = _locManager.LoadLanguage(langCode);
        var newDict = new Avalonia.Controls.ResourceDictionary();
        foreach (var kvp in translations) if (kvp.Key != "_LangName") newDict[kvp.Key] = kvp.Value;

        var app = Avalonia.Application.Current;
        if (app != null)
        {
            if (_currentLangDict != null) app.Resources.MergedDictionaries.Remove(_currentLangDict);
            app.Resources.MergedDictionaries.Add(newDict);
            _currentLangDict = newDict;
        }

        BuildColumns();
    }

    private void BuildColumns()
    {
        if (FilesSource == null) return;

        var t = _locManager.LoadLanguage(Settings.Language);
        FilesSource.Columns.Clear();

        FilesSource.Columns.Add(new TemplateColumn<FileSystemItemDisplay>(
            t.TryGetValue("ColName", out var n) ? n : "Name",
            "NameCellTemplate",
            width: new GridLength(250),
            options: new TemplateColumnOptions<FileSystemItemDisplay>
            {
                CompareAscending = (a, b) => string.Compare(a?.Name, b?.Name, StringComparison.CurrentCultureIgnoreCase),
                CompareDescending = (a, b) => string.Compare(b?.Name, a?.Name, StringComparison.CurrentCultureIgnoreCase)
            }));

        if (HasAudioMetadata)
        {
            FilesSource.Columns.Add(new TextColumn<FileSystemItemDisplay, string>(
                t.TryGetValue("ColArtist", out var a) ? a : "Artist", x => x.Artist, width: new GridLength(150),
                options: new TextColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => string.Compare(a?.Artist, b?.Artist, StringComparison.CurrentCultureIgnoreCase),
                    CompareDescending = (a, b) => string.Compare(b?.Artist, a?.Artist, StringComparison.CurrentCultureIgnoreCase)
                }));

            FilesSource.Columns.Add(new TextColumn<FileSystemItemDisplay, string>(
                t.TryGetValue("ColTitle", out var ti) ? ti : "Title", x => x.Title, width: new GridLength(150),
                options: new TextColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => string.Compare(a?.Title, b?.Title, StringComparison.CurrentCultureIgnoreCase),
                    CompareDescending = (a, b) => string.Compare(b?.Title, a?.Title, StringComparison.CurrentCultureIgnoreCase)
                }));
        }

        if (ShowExtensionColumn)
        {
            FilesSource.Columns.Add(new TextColumn<FileSystemItemDisplay, string>(
                t.TryGetValue("ColExt", out var e) ? e : "Ext", x => x.Extension, width: new GridLength(80),
                options: new TextColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => string.Compare(a?.Extension, b?.Extension, StringComparison.CurrentCultureIgnoreCase),
                    CompareDescending = (a, b) => string.Compare(b?.Extension, a?.Extension, StringComparison.CurrentCultureIgnoreCase)
                }));
        }

        if (ShowSizeColumn)
        {
            FilesSource.Columns.Add(new TemplateColumn<FileSystemItemDisplay>(
                t.TryGetValue("ColSize", out var s) ? s : "Size",
                "SizeCellTemplate",
                width: new GridLength(100),
                options: new TemplateColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => Nullable.Compare(a?.SizeBytes, b?.SizeBytes),
                    CompareDescending = (a, b) => Nullable.Compare(b?.SizeBytes, a?.SizeBytes)
                }));
        }

        if (ShowModifiedDateColumn)
        {
            FilesSource.Columns.Add(new TemplateColumn<FileSystemItemDisplay>(
                t.TryGetValue("ColDate", out var d) ? d : "Date",
                "DateCellTemplate",
                width: new GridLength(150),
                options: new TemplateColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => Nullable.Compare(a?.ModifiedTicks, b?.ModifiedTicks),
                    CompareDescending = (a, b) => Nullable.Compare(b?.ModifiedTicks, a?.ModifiedTicks)
                }));
        }

        if (ShowPathColumn)
        {
            FilesSource.Columns.Add(new TextColumn<FileSystemItemDisplay, string>(
                t.TryGetValue("ColPath", out var p) ? p : "Path", x => x.Path, width: new GridLength(200),
                options: new TextColumnOptions<FileSystemItemDisplay>
                {
                    CompareAscending = (a, b) => string.Compare(a?.Path, b?.Path, StringComparison.CurrentCultureIgnoreCase),
                    CompareDescending = (a, b) => string.Compare(b?.Path, a?.Path, StringComparison.CurrentCultureIgnoreCase)
                }));
        }
    }

    private void UpdateTitle()
    {
        string fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
        WindowTitle = $"File Catalog - {fileName}{(HasUnsavedChanges ? "*" : "")}";
    }

    private void MarkAsDirty() { HasUnsavedChanges = true; UpdateTitle(); }

    [RelayCommand] private void ToggleSettings() { if (IsSettingsOpen) SaveSettings(); IsSettingsOpen = !IsSettingsOpen; }

    [RelayCommand] private void ToggleStatusHistory() => IsStatusHistoryOpen = !IsStatusHistoryOpen;

    public void TriggerExitWarning() { _pendingAction = PendingAction.Exit; IsUnsavedWarningOpen = true; }
    [RelayCommand] private void Quit() { if (HasUnsavedChanges) TriggerExitWarning(); else RequestApplicationClose?.Invoke(); }

    [RelayCommand] private async Task NewCatalogAsync() { if (HasUnsavedChanges) { _pendingAction = PendingAction.New; IsUnsavedWarningOpen = true; return; } await ExecuteNewCatalogAsync(); }
    [RelayCommand] private async Task OpenCatalogAsync() { if (HasUnsavedChanges) { _pendingAction = PendingAction.Open; IsUnsavedWarningOpen = true; return; } await ExecuteOpenCatalogAsync(); }

    [RelayCommand]
    private async Task ConfirmUnsavedWarningAsync(string choice)
    {
        IsUnsavedWarningOpen = false;
        if (choice == "Cancel") { _pendingAction = PendingAction.None; return; }
        if (choice == "Save")
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                if (_dialogService == null) return;
                string? dest = await _dialogService.ShowSaveFileDialogAsync();
                if (string.IsNullOrEmpty(dest)) { _pendingAction = PendingAction.None; return; }
                await PerformSaveAsync(dest);
            }
            else { await PerformSaveAsync(_currentFilePath); }
        }

        if (_pendingAction == PendingAction.Exit) RequestApplicationClose?.Invoke();
        else if (_pendingAction == PendingAction.New) await ExecuteNewCatalogAsync();
        else if (_pendingAction == PendingAction.Open) await ExecuteOpenCatalogAsync();
        _pendingAction = PendingAction.None;
    }

    private async Task ExecuteNewCatalogAsync()
    {
        IsBusy = true; StatusMessage = "Creating new catalog...";
        try
        {
            await Task.Run(() =>
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);
                new DatabaseInitializer().Initialize(_tempFilePath);
                _repository.ChangeDatabase(_tempFilePath);
            });
            _currentFilePath = null; HasUnsavedChanges = false;
            Settings.LastCatalogPath = null; SaveSettings();
            UpdateTitle(); await LoadInitialDataAsync(); StatusMessage = "New catalog created.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ExecuteOpenCatalogAsync()
    {
        if (_dialogService == null || IsBusy) return;
        string? selectedPath = await _dialogService.ShowOpenFileDialogAsync();
        if (string.IsNullOrEmpty(selectedPath)) return;

        IsBusy = true; StatusMessage = $"Opening {Path.GetFileName(selectedPath)}...";
        try
        {
            // Nový kompresní engine
            await _backupService.LoadCatalogFromFileAsync(selectedPath, _tempFilePath);
            _currentFilePath = selectedPath; HasUnsavedChanges = false;
            Settings.LastCatalogPath = selectedPath; SaveSettings();
            _repository.ChangeDatabase(_tempFilePath);
            await LoadInitialDataAsync(); UpdateTitle(); StatusMessage = "Catalog opened successfully.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task SaveCatalogAsync() { if (string.IsNullOrEmpty(_currentFilePath)) { await SaveCatalogAsAsync(); return; } await PerformSaveAsync(_currentFilePath); }
    [RelayCommand] private async Task SaveCatalogAsAsync() { if (_dialogService == null || IsBusy) return; string? destinationPath = await _dialogService.ShowSaveFileDialogAsync(); if (string.IsNullOrEmpty(destinationPath)) return; await PerformSaveAsync(destinationPath); }

    private async Task PerformSaveAsync(string destinationPath)
    {
        IsBusy = true; StatusMessage = "Saving catalog...";
        try
        {
            // Nový kompresní engine
            await _backupService.SaveCatalogToFileAsync(_tempFilePath, destinationPath);
            _currentFilePath = destinationPath; HasUnsavedChanges = false;
            Settings.LastCatalogPath = destinationPath; SaveSettings();
            UpdateTitle(); StatusMessage = "Catalog saved successfully.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    partial void OnSearchQueryChanged(string value) => ProcessSearchQueryChangedAsync(value).SafeFireAndForget(ex =>
    {
        StatusMessage = $"Search error: {ex.Message}";
        _logger.LogErrorAsync("SearchQuery handler failed", ex).SafeFireAndForget();
    });

    private async Task ProcessSearchQueryChangedAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value) && !_isNavigating)
        {
            if (_lastSelectedFolder != null)
            {
                SelectedFolder = _lastSelectedFolder;
                await LoadFilesForFolderAsync(_lastSelectedFolder);
            }
            else
            {
                Items.Clear();
                HasAudioMetadata = false;
            }
        }
    }

    partial void OnSelectedItemChanged(FileSystemItemDisplay? value) => ProcessSelectedItemChangedAsync(value).SafeFireAndForget(ex =>
    {
        StatusMessage = $"Navigation error: {ex.Message}";
        _logger.LogErrorAsync("SelectedItem handler failed", ex).SafeFireAndForget();
    });

    private async Task ProcessSelectedItemChangedAsync(FileSystemItemDisplay? value)
    {
        if (value != null && !string.IsNullOrWhiteSpace(SearchQuery) && !_isNavigating) await JumpToFolderAsync(value.FolderId);
    }

    partial void OnSelectedFolderChanged(FolderNodeViewModel? value) => ProcessSelectedFolderChangedAsync(value).SafeFireAndForget(ex =>
    {
        StatusMessage = $"Folder loading error: {ex.Message}";
        _logger.LogErrorAsync("SelectedFolder handler failed", ex).SafeFireAndForget();
    });

    private async Task ProcessSelectedFolderChangedAsync(FolderNodeViewModel? value)
    {
        if (value == null || _isNavigating) return;
        await LoadFilesForFolderAsync(value);
    }

    private async Task LoadFilesForFolderAsync(FolderNodeViewModel folderNode)
    {
        var buffer = new List<FileSystemItemDisplay>();

        var folders = await _repository.GetSubFoldersAsync(folderNode.Folder.DriveId, folderNode.Folder.Id);
        foreach (var f in folders) buffer.Add(new FileSystemItemDisplay { IsFolder = true, Name = f.Name, Extension = "folder", Path = folderNode.Folder.RelativePath, FolderId = f.Id });

        var files = await _repository.GetFilesAsync(folderNode.Folder.Id);
        foreach (var file in files) buffer.Add(new FileSystemItemDisplay { IsFolder = false, Name = file.Name, Extension = file.Extension, SizeBytes = file.SizeBytes, ModifiedTicks = file.ModifiedTicks, Path = folderNode.Folder.RelativePath, FolderId = folderNode.Folder.Id, Title = file.Title, Artist = file.Artist });

        HasAudioMetadata = files.Any(f => !string.IsNullOrEmpty(f.Title) || !string.IsNullOrEmpty(f.Artist));

        Items.Clear();
        Items.AddRange(buffer);

        if (Settings.AutoCalculateFolderSizes)
        {
            foreach (var item in Items.Where(i => i.IsFolder))
            {
                CalculateFolderSizeInternalAsync(item, silent: true).SafeFireAndForget(ex =>
                    _logger.LogErrorAsync("Auto-calc folder size failed", ex).SafeFireAndForget());
            }
        }
    }

    public async Task LoadInitialDataAsync()
    {
        await _repository.InitializeDatabaseSchemaAsync();

        RootFolders.Clear(); Items.Clear(); SelectedFolder = null; HasAudioMetadata = false;
        var drives = await _repository.GetDrivesAsync();
        foreach (var drive in drives)
        {
            var rootFoldersForDrive = await _repository.GetSubFoldersAsync(drive.Id, null);
            foreach (var folder in rootFoldersForDrive) RootFolders.Add(new FolderNodeViewModel(folder, _repository, true) { Drive = drive });
        }
    }

    [RelayCommand]
    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || IsBusy) return;
        if (SelectedFolder != null) _lastSelectedFolder = SelectedFolder;
        SelectedFolder = null;

        var results = await _repository.SearchFilesAsync(SearchQuery, UseRegex);
        HasAudioMetadata = results.Any(f => !string.IsNullOrEmpty(f.Title) || !string.IsNullOrEmpty(f.Artist));

        Items.Clear();
        Items.AddRange(results);
    }

    [RelayCommand] private async Task OpenFolderFromGridAsync(FileSystemItemDisplay? item) { if (item == null || !item.IsFolder) return; await JumpToFolderAsync(item.FolderId); }

    private async Task JumpToFolderAsync(int targetFolderId)
    {
        _isNavigating = true; SearchQuery = string.Empty;
        var pathIds = await _repository.GetFolderPathIdsAsync(targetFolderId);
        ObservableCollection<FolderNodeViewModel> currentLevel = RootFolders;
        FolderNodeViewModel? targetNode = null;
        foreach (var id in pathIds)
        {
            targetNode = currentLevel.FirstOrDefault(n => n.Folder.Id == id);
            if (targetNode != null) { await targetNode.ExpandAsync(); currentLevel = targetNode.SubFolders; } else break;
        }
        if (targetNode != null) { SelectedFolder = targetNode; _lastSelectedFolder = targetNode; await LoadFilesForFolderAsync(targetNode); }
        _isNavigating = false;
    }

    [RelayCommand]
    private async Task ScanNewDriveAsync()
    {
        if (_dialogService == null || IsBusy) return;
        string? selectedPath = await _dialogService.ShowFolderPickerDialogAsync();
        if (string.IsNullOrEmpty(selectedPath)) return;

        IsBusy = true; StatusMessage = $"Scanning {selectedPath}...";
        try
        {
            string driveName = new DirectoryInfo(selectedPath).Name;
            if (string.IsNullOrEmpty(driveName)) driveName = selectedPath;

            await Task.Run(async () =>
            {
                int driveId = await _repository.GetOrCreateDriveAsync(driveName, selectedPath);
                await _repository.ClearDriveContentsAsync(driveId);
                var scanner = new DiskScannerService(_tempFilePath, Settings, _logger);
                await scanner.ScanAndSaveAsync(selectedPath, driveId);
            });

            await LoadInitialDataAsync(); MarkAsDirty(); StatusMessage = "Scan completed successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            await _logger.LogErrorAsync("Scan failed", ex);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void BeginRename(FolderNodeViewModel? node) { if (node != null) node.IsEditing = true; }
    [RelayCommand] private void EndRename(FolderNodeViewModel? node) { if (node != null) { node.IsEditing = false; MarkAsDirty(); } }

    [RelayCommand]
    private async Task RemoveDriveAsync(FolderNodeViewModel? node)
    {
        if (node == null || !node.IsDriveRoot || node.Drive == null || IsBusy) return;
        await _repository.DeleteDriveAsync(node.Drive.Id); RootFolders.Remove(node);
        if (SelectedFolder == node) SelectedFolder = null; Items.Clear(); HasAudioMetadata = false; MarkAsDirty();
    }

    [RelayCommand]
    private async Task RescanDriveAsync(FolderNodeViewModel? node)
    {
        if (node == null || !node.IsDriveRoot || node.Drive == null || IsBusy) return;
        IsBusy = true; StatusMessage = $"Rescanning {node.Drive.Identifier}...";
        try
        {
            await Task.Run(async () =>
            {
                await _repository.ClearDriveContentsAsync(node.Drive.Id);
                var scanner = new DiskScannerService(_tempFilePath, Settings, _logger);
                await scanner.ScanAndSaveAsync(node.Drive.Identifier, node.Drive.Id);
            });
            await LoadInitialDataAsync(); MarkAsDirty(); StatusMessage = "Rescan completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rescan failed: {ex.Message}";
            await _logger.LogErrorAsync("Rescan failed", ex);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task MoveDriveUpAsync(FolderNodeViewModel? node) { if (node == null || !node.IsDriveRoot || node.Drive == null) return; int index = RootFolders.IndexOf(node); if (index > 0) { RootFolders.Move(index, index - 1); await SaveDriveOrderAsync(); MarkAsDirty(); } }
    [RelayCommand] private async Task MoveDriveDownAsync(FolderNodeViewModel? node) { if (node == null || !node.IsDriveRoot || node.Drive == null) return; int index = RootFolders.IndexOf(node); if (index >= 0 && index < RootFolders.Count - 1) { RootFolders.Move(index, index + 1); await SaveDriveOrderAsync(); MarkAsDirty(); } }
    private async Task SaveDriveOrderAsync() { for (int i = 0; i < RootFolders.Count; i++) { if (RootFolders[i].Drive != null) { RootFolders[i].Drive!.SortOrder = i; await _repository.UpdateDriveSortOrderAsync(RootFolders[i].Drive!.Id, i); } } }

    [RelayCommand] private async Task CalculateFolderSizeAsync(FileSystemItemDisplay? item) { await CalculateFolderSizeInternalAsync(item, silent: false); }
    private async Task CalculateFolderSizeInternalAsync(FileSystemItemDisplay? item, bool silent)
    {
        if (item == null || !item.IsFolder) return;
        if (!silent) { IsBusy = true; StatusMessage = $"Calculating size for {item.Name}..."; }
        try
        {
            long totalSize = await _repository.GetFolderTotalSizeAsync(item.FolderId); item.SizeBytes = totalSize;
            if (!silent) StatusMessage = "Folder size calculated.";
        }
        catch (Exception ex)
        {
            if (!silent) StatusMessage = $"Calculation failed: {ex.Message}";
            await _logger.LogErrorAsync("Calculate folder size failed", ex);
        }
        finally { if (!silent) IsBusy = false; }
    }
}