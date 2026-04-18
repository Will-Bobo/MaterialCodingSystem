using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Preferences;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Infrastructure.Storage;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;
using MaterialCodingSystem.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MaterialCodingSystem;

public partial class App : System.Windows.Application
{
    public static ServiceProvider Services { get; private set; } = null!;
    private ILogger<App>? _startupLogger;
    private string? _finalLogDirectory;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        ConfigureLogging(sc);
        RegisterGlobalExceptionLogging();

        sc.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();

        // 组合根在 BuildServiceProvider 前需要 dbPath，因此此处临时 new 一次。
        // 后续业务层获取路径时必须通过 DI 的 IDatabasePathProvider（不允许硬编码/多来源）。
        var tmpPathProvider = new DatabasePathProvider();
        var dbPath = tmpPathProvider.GetMainDbPath();
        var dataDir = Path.GetDirectoryName(dbPath)!;
        var prefsPath = Path.Combine(dataDir, "preferences.json");

        sc.AddSingleton<JsonExportPathPreferenceStore>(_ => new JsonExportPathPreferenceStore(prefsPath));
        sc.AddSingleton<IExportPathPreferenceStore>(sp => sp.GetRequiredService<JsonExportPathPreferenceStore>());
        sc.AddSingleton<IBomArchivePreferenceStore>(sp => sp.GetRequiredService<JsonExportPathPreferenceStore>());
        sc.AddSingleton<IUiDialogService, WpfUiDialogService>();
        sc.AddSingleton<IUiRenderer, WpfUiRenderer>();
        sc.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        sc.AddSingleton<IFileSaveDialog, WpfSaveExcelFileDialog>();
        sc.AddSingleton<IFileDbSaveDialog, WpfSaveDbFileDialog>();
        sc.AddSingleton<IFileOpenDialog, WpfOpenDbFileDialog>();
        sc.AddSingleton<IBomExcelOpenFileDialog, WpfOpenBomExcelFileDialog>();
        sc.AddSingleton<IBomArchiveInteraction, WpfBomArchiveInteraction>();
        sc.AddSingleton<IRestoreReadOnlyLockNotifier, WpfRestoreReadOnlyLockNotifier>();
        sc.AddSingleton<IExcelMaterialExporter, ClosedXmlMaterialExcelExporter>();
        sc.AddSingleton<IDebouncer>(_ => new WpfDebouncer(Dispatcher.CurrentDispatcher));
        sc.AddSingleton<IAppExecutionDirectoryProvider, AppExecutionDirectoryProvider>();
        sc.AddSingleton<IFileSystemBomArchiveStorage, FileSystemBomArchiveStorage>();
        sc.AddTransient<IBomArchiveRepository, SqliteBomArchiveRepository>();
        // BOM Excel 解析（统一解析器：Adapter + Domain Rules）
        sc.AddSingleton<MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter>();
        sc.AddSingleton<MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter>();
        sc.AddSingleton<IBomGridParser, UnifiedBomGridParser>();
        sc.AddTransient(sp => new ParseBomUseCase(
            sp.GetRequiredService<IBomGridParser>(),
            sp.GetRequiredService<ILogger<ParseBomUseCase>>()));
        sc.AddSingleton<IBomFileFormatDetector, BomFileFormatDetector>();

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
                sp.GetRequiredService<IExcelMaterialExporter>(),
                sp.GetRequiredService<ILogger<MaterialApplicationService>>()));

        sc.AddTransient(sp => new AnalyzeBomUseCase(
            sp.GetRequiredService<ParseBomUseCase>(),
            sp.GetRequiredService<IBomFileFormatDetector>(),
            sp.GetRequiredService<SqliteMaterialRepository>(),
            sp.GetRequiredService<SqliteUnitOfWork>(),
            sp.GetRequiredService<ILogger<AnalyzeBomUseCase>>()));
        sc.AddTransient<BomArchiveService>();
        sc.AddSingleton<BomImportInProgressGate>();
        sc.AddTransient<CanArchiveBomUseCase>();
        sc.AddTransient<ConfigureBomArchiveRootPathUseCase>();
        sc.AddTransient<ArchiveBomUseCase>();
        sc.AddTransient<GetBomArchiveListUseCase>();
        sc.AddTransient<ImportBomNewMaterialsUseCase>();
        sc.AddTransient<ValidateBomArchiveIntegrityUseCase>();

        sc.AddSingleton<MainViewModel>();

        Services = sc.BuildServiceProvider();

        TryWriteStartupLog();

        var window = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        window.Show();

        _ = Task.Run(() => Services.GetRequiredService<StartupOrchestrationService>().OnAppStartedAsync());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
            // 必须保证退出流程不被日志影响
        }

        base.OnExit(e);
    }

    private void ConfigureLogging(ServiceCollection sc)
    {
        // V1.1：日志必须优先落盘到 %LocalAppData%\MaterialCodingSystem\logs；失败不得影响启动
        var logDir = GetPreferredLogDirectory();

        try
        {
            Directory.CreateDirectory(logDir);
            _finalLogDirectory = logDir;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 20 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            sc.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: false);
            });

            var factory = new SerilogLoggerFactory(Log.Logger, dispose: false);
            _startupLogger = factory.CreateLogger<App>();
            _startupLogger.LogInformation("Logging initialized. log_dir={logDir}", _finalLogDirectory);
        }
        catch (Exception ex)
        {
            _finalLogDirectory = null;

            // Fallback：保持旧行为（Debug provider），并保证启动继续
            sc.AddLogging(b => b.AddDebug());

            try
            {
                var fallbackFactory = LoggerFactory.Create(b => b.AddDebug());
                _startupLogger = fallbackFactory.CreateLogger<App>();
                _startupLogger.LogWarning(ex, "Logging initialization failed. continue without file logging. preferred_log_dir={logDir}", logDir);
            }
            catch
            {
                // 最终兜底：什么都不做，确保启动不受影响
            }
        }
    }

    private static string GetPreferredLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(Path.GetTempPath(), "MaterialCodingSystem", "logs");

        return Path.Combine(localAppData, "MaterialCodingSystem", "logs");
    }

    private void RegisterGlobalExceptionLogging()
    {
        // DispatcherUnhandledException：UI 线程未处理异常
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                _startupLogger?.LogCritical(args.Exception, "Fatal exception (DispatcherUnhandledException).");
            }
            catch
            {
                // 必须保证异常处理不被日志影响
            }
            // 不改变现有行为：不主动设置 Handled
        };

        // AppDomain.CurrentDomain.UnhandledException：非 UI 线程未处理异常
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                if (args.ExceptionObject is Exception ex)
                    _startupLogger?.LogCritical(ex, "Fatal exception (UnhandledException). is_terminating={isTerminating}", args.IsTerminating);
                else
                    _startupLogger?.LogCritical("Fatal exception (UnhandledException). is_terminating={isTerminating} exception_object={exceptionObject}", args.IsTerminating, args.ExceptionObject);
            }
            catch
            {
            }
        };

        // TaskScheduler.UnobservedTaskException：未观察到的 Task 异常
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                _startupLogger?.LogCritical(args.Exception, "Fatal exception (UnobservedTaskException).");
            }
            catch
            {
            }

            try
            {
                args.SetObserved();
            }
            catch
            {
            }
        };
    }

    private void TryWriteStartupLog()
    {
        try
        {
            _startupLogger?.LogInformation("App Start.");
            _startupLogger?.LogInformation("Log Path. log_dir={logDir}", _finalLogDirectory ?? "(not available)");
        }
        catch
        {
            // 启动日志失败不得影响启动
        }
    }
}
