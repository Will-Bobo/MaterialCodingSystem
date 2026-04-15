namespace MaterialCodingSystem.Application.Interfaces;

public interface IDatabaseConnectionCloser
{
    Task CloseAsync(CancellationToken ct = default);
}

