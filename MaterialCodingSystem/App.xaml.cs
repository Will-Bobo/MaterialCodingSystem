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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MaterialCodingSystem;

public partial class App : System.Windows.Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        sc.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();

        // 组合根在 BuildServiceProvider 前需要 dbPath，因此此处临时 new 一次。
        // 后续业务层获取路径时必须通过 DI 的 IDatabasePathProvider（不允许硬编码/多来源）。
        var tmpPathProvider = new DatabasePathProvider();
        var dbPath = tmpPathProvider.GetMainDbPath();
        var dataDir = Path.GetDirectoryName(dbPath)!;
        var prefsPath = Path.Combine(dataDir, "preferences.json");

        sc.AddSingleton<IExportPathPreferenceStore>(_ => new JsonExportPathPreferenceStore(prefsPath));
        sc.AddSingleton<IUiDialogService, WpfUiDialogService>();
        sc.AddSingleton<IUiRenderer, WpfUiRenderer>();
        sc.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        sc.AddSingleton<IFileSaveDialog, WpfSaveExcelFileDialog>();
        sc.AddSingleton<IFileDbSaveDialog, WpfSaveDbFileDialog>();
        sc.AddSingleton<IFileOpenDialog, WpfOpenDbFileDialog>();
        sc.AddSingleton<IRestoreReadOnlyLockNotifier, WpfRestoreReadOnlyLockNotifier>();
        sc.AddSingleton<IExcelMaterialExporter, ClosedXmlMaterialExcelExporter>();
        sc.AddSingleton<IDebouncer>(_ => new WpfDebouncer(Dispatcher.CurrentDispatcher));

        // dbPath 来源必须唯一：%LocalAppData%\MaterialCodingSystem\mcs.db（由 IDatabasePathProvider 提供）
        sc.AddPersistence(dbPath);

        sc.AddTransient<SqliteUnitOfWork>();
        sc.AddTransient<SqliteMaterialRepository>();
        sc.AddTransient<IBackupRepository, SqliteBackupRepository>();
        sc.AddTransient<IDatabaseConnectionCloser, SqliteConnectionCloser>();
        sc.AddSingleton<MaintenanceOperationGate>();
        sc.AddTransient<DatabaseBackupService>();
        sc.AddTransient<StartupOrchestrationService>();
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

        _ = Task.Run(() => Services.GetRequiredService<StartupOrchestrationService>().OnAppStartedAsync());
    }
}
