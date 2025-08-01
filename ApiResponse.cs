

namespace LoggingAPI.Models
{
    public class ApiResponse
    {
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? Error { get; set; }
    }
}