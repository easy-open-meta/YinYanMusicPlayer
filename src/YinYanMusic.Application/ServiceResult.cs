namespace YinYanMusic.Application;

public sealed class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class ServiceResult<T>
{
    public T? Data { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Data = data, Success = true };
    public static ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
}