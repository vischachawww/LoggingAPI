using System.ComponentModel.DataAnnotations;
//import validation attributes like [Required][Range]

namespace LoggingAPI.Models
//grouping this class logically w other model-related code
{
    public class LogEntry
    {
        [Required]
        public DateTime Timestamp { get; set; }
        [Required]
        //case sensitive
        [RegularExpression("(?i)^(INFO|WARN|ERROR|DEBUG)$", ErrorMessage = "Level must be one of: INFO, WARN, ERROR, DEBUG")]
        public string Level { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        [Required]
        public string Source { get; set; } = string.Empty;
        public string UserID { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        [Range(100, 599)] //int default value is 0, but 0 is outside of range. no status in log = validation fail
        public int Status { get; set; }

        //optional 
        public string? ErrorCode { get; set; }  //application-specific error identifier
        public string? StackTrace { get; set; }  //detailed error trace (for ERROR logs)
        public string? RequestId { get; set; }   //to trace specific requests
    }
}