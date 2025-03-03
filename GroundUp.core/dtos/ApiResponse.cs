namespace GroundUp.core.dtos
{
    public class ApiResponse<T>
    {
        public T Data { get; set; }
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Success";
        public List<string>? Errors { get; set; } // Optional error messages
        public int StatusCode { get; set; }
        public string? ErrorCode { get; set; }

        public ApiResponse(T data, bool success = true, string message = "Success", List<string>? errors = null, int statusCode = 200, string? errorCode = null)
        {
            Data = data;
            Success = success;
            Message = message;
            Errors = errors;
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
}