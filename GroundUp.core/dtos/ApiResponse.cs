namespace GroundUp.core.dtos
{
    public class ApiResponse<T>
    {
        public T Data { get; set; }
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Success";
        public List<string>? Errors { get; set; } // Optional error messages

        public ApiResponse(T data, bool success = true, string message = "Success", List<string>? errors = null)
        {
            Data = data;
            Success = success;
            Message = message;
            Errors = errors;
        }
    }
}