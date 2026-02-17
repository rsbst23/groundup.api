namespace GroundUp.Core.dtos;

public class OperationResult<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "Success";
    public List<string>? Errors { get; set; }
    public int StatusCode { get; set; } = 200;
    public string? ErrorCode { get; set; }

    public static OperationResult<T> Ok(T? data, string message = "Success", int statusCode = 200)
        => new() { Data = data, Success = true, Message = message, StatusCode = statusCode };

    public static OperationResult<T> Fail(
        string message,
        int statusCode,
        string? errorCode = null,
        List<string>? errors = null,
        T? data = default)
        => new()
        {
            Data = data,
            Success = false,
            Message = message,
            StatusCode = statusCode,
            ErrorCode = errorCode,
            Errors = errors
        };
}
