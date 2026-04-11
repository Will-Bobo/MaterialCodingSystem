namespace MaterialCodingSystem.Application.Interfaces;

public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}

