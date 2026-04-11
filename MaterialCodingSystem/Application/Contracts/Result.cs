namespace MaterialCodingSystem.Application.Contracts;

public sealed record AppError(string Code, string Message);

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public AppError? Error { get; }

    private Result(bool isSuccess, T? data, AppError? error)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
    }

    public static Result<T> Ok(T data) => new(true, data, null);
    public static Result<T> Fail(string code, string message) => new(false, default, new AppError(code, message));
}

