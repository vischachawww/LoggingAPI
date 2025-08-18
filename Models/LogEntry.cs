using System.ComponentModel.DataAnnotations;
//import validation attributes like [Required][Range]
using System.Text.Json.Serialization;

namespace LoggingAPI.Models
//grouping this class logically w other model-related code
{
    public class LogEntry
    {
        [Required]
        [JsonPropertyName("@timestamp")] // Critical for ES mapping
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        [Required]
        //case sensitive
        [RegularExpression("(?i)^(INFO|WARN|ERROR|DEBUG)$", ErrorMessage = "Level must be one of: INFO, WARN, ERROR, DEBUG")]
         [JsonPropertyName("Level")]
        public string Level { get; set; } = string.Empty;
        [Required]
         [JsonPropertyName("Message")]
        public string Message { get; set; } = string.Empty;
         [Required]
         [JsonPropertyName("CorrelationId")]
        public string CorrelationId { get; set; } = string.Empty;
         [Required]
         [JsonPropertyName("Elapsed")]
        public string Elapsed { get; set; } = string.Empty;
          [JsonPropertyName("Source")]
        public string Source { get; set; } = string.Empty;
        [JsonPropertyName("UserID")]
        public string UserID { get; set; } = string.Empty;
          [JsonPropertyName("RequestPath")]
        public string RequestPath { get; set; } = string.Empty;
          [JsonPropertyName("Requester")]
        public string Requester { get; set; } = string.Empty;
          [JsonPropertyName("RequestId")]
        public string RequestId { get; set; } = string.Empty;
        [JsonPropertyName("Environment")]
        public string Environment { get; set; } = string.Empty;
        [Range(100, 599)] 
          [JsonPropertyName("Status")]
        public int Status { get; set; }
        //  [JsonExtensionData]
        //  public Dictionary<string, object> Metadata { get; set; } = new();

        //optional 
        public string? ErrorCode { get; set; }  //application-specific error identifier
        public string? StackTrace { get; set; }  //detailed error trace (for ERROR logs)
       
    }
}