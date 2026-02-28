using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileCatalog.ViewModels;

namespace FileCatalog.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // OPRAVA: Propojení povelu k ukonèení z ViewModelu s fyzickım zavøením okna
            vm.RequestApplicationClose = () => Close();
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

    // OPRAVA: Bezpeènı úklid pøi ukonèení aplikace (zavøení køíkem i pøes File > Quit)
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try
        {
            // Kritické: Uvolníme vlákna a pamìové zámky SQLite enginu, 
            // jinak by nám operaèní systém nedovolil soubor smazat.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            string tempPath = Path.Combine(Path.GetTempPath(), "FileCatalog_temp.kat");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // V pøípadì kolize (napø. probíhající antivirovı sken) mùeme chybu tiše ignorovat.
            // Jeliko je soubor v systémovém Tempu, Windows se o jeho smazání postarají pozdìji.
        }
    }
}