using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileCatalog.Services.Core;
using FileCatalog.Services.Database;
using FileCatalog.Services.Localization;
using FileCatalog.Services.Settings;
using FileCatalog.Services.UI;
using FileCatalog.ViewModels;
using FileCatalog.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FileCatalog;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // 1. Core Services registration
        services.AddSingleton<PathProvider>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<LocalizationManager>();
        services.AddTransient<DatabaseBackupService>();
        services.AddSingleton<AppLogger>();

        // Temporarily build provider to resolve paths for DB initialization
        var tempProvider = services.BuildServiceProvider();
        var pathProvider = tempProvider.GetRequiredService<PathProvider>();

        // 2. Repository and ViewModel registration
        services.AddSingleton<CatalogRepository>(sp => new CatalogRepository(pathProvider.TempDatabasePath));
        services.AddTransient<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();

            services.AddSingleton<IDialogService>(new DialogService(mainWindow, pathProvider));

            // 3. Seal the container locally (Composition Root). 
            // The global static ServiceLocator anti-pattern has been entirely removed.
            var serviceProvider = services.BuildServiceProvider();

            mainWindow.DataContext = serviceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}