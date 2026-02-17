namespace MediaRankerServer.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        public static ApiResponse<T> Ok(T data) => new()
        {
            Success = true,
            Data = data
        };

        public static ApiResponse<T> Fail(string message, Dictionary<string, string[]>? errors = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}
