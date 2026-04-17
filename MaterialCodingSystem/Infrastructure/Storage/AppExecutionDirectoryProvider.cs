using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Storage;

public sealed class AppExecutionDirectoryProvider : IAppExecutionDirectoryProvider
{
    public string GetExecutionDirectory() => AppContext.BaseDirectory;
}

