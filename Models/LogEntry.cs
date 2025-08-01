using System.ComponentModel.DataAnnotations;
namespace LoggingAPI.Models
{
    public class LogEntry
    {
        [Required]
        public DateTime Timestamp { get; set; }
        [Required]
        [RegularExpression("(?i)^(INFO|WARN|ERROR|DEBUG)$", ErrorMessage = "Level must be one of: INFO, WARN, ERROR, DEBUG")]
        public string Level { get; set; } = string.Empty;
        [Required]
        public string Service { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        [Range(100, 599)]
        public int Status { get; set; }
        public string? ErrorCode { get; set; }
        public string? StackTrace { get; set; }
        public string? RequestId { get; set; }
    }
}