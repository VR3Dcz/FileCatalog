using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileCatalog.ViewModels;

namespace FileCatalog.Views;

public partial class MainWindow : Window
{
    // Safety flag to prevent an infinite loop when closing via the View Model
    private bool _isForceClosing;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RequestApplicationClose = () =>
            {
                _isForceClosing = true;
                Close();
            };
            await vm.InitializeStartupAsync();
        }
    }

    private void MainDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedItem != null)
        {
            if (vm.OpenFolderFromGridCommand.CanExecute(vm.SelectedItem))
            {
                vm.OpenFolderFromGridCommand.Execute(vm.SelectedItem);
            }
        }
    }

    private void StatusBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToggleStatusHistoryCommand.Execute(null);
        }
    }

    // INTERCEPT THE OS WINDOW CLOSE BUTTON ('X')
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // If the close didn't originate from our confirmed dialog and there are unsaved changes
        if (!_isForceClosing && DataContext is MainViewModel { HasUnsavedChanges: true } vm)
        {
            e.Cancel = true; // Stop the OS from killing the window
            vm.TriggerExitWarning(); // Show our custom unsaved changes warning
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            string tempPath = Path.Combine(Path.GetTempPath(), "FileCatalog_temp.kat");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {

        }
    }
}