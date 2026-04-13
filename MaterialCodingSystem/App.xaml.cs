using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Preferences;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;
using MaterialCodingSystem.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MaterialCodingSystem;

public partial class App : System.Windows.Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        var dataDir = Path.Combine(AppContext.BaseDirectory, "MaterialCodingSystem");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "mcs.db");
        var prefsPath = Path.Combine(dataDir, "preferences.json");

        sc.AddSingleton<IExportPathPreferenceStore>(_ => new JsonExportPathPreferenceStore(prefsPath));
        sc.AddSingleton<IUiRenderer, WpfUiRenderer>();
        sc.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        sc.AddSingleton<IFileSaveDialog, WpfSaveExcelFileDialog>();
        sc.AddSingleton<IExcelMaterialExporter, ClosedXmlMaterialExcelExporter>();
        sc.AddSingleton<IDebouncer>(_ => new WpfDebouncer(Dispatcher.CurrentDispatcher));

        sc.AddPersistence(dbPath);

        sc.AddTransient<SqliteUnitOfWork>();
        sc.AddTransient<SqliteMaterialRepository>();
        sc.AddTransient(sp =>
            new MaterialApplicationService(
                sp.GetRequiredService<SqliteUnitOfWork>(),
                sp.GetRequiredService<SqliteMaterialRepository>(),
                sp.GetRequiredService<IExcelMaterialExporter>()));

        sc.AddSingleton<MainViewModel>();

        Services = sc.BuildServiceProvider();

        var window = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        window.Show();
    }
}
