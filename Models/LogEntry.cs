using System.ComponentModel.DataAnnotations;
using System.Text.Json;

//import validation attributes like [Required][Range]
using System.Text.Json.Serialization;
using Nest;
//using Newtonsoft.Json;
namespace LoggingAPI.Models

//grouping this class logically w other model-related code
{
  public class LogEntry
  {
    [PropertyName("@timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    [Required(ErrorMessage = "CorrelationId is required")]
    public string CorrelationId { get; set; }
    [Required(ErrorMessage = "RemoteServerIp is required")]
    [RegularExpression(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", ErrorMessage = "Invalid IP format")]
    public string RemoteServerIp { get; set; }
  [Required(ErrorMessage = "RequestBody is required")]
  [JsonPropertyName("requestBody")]
    public object RequestBody { get; set; }
    [Required(ErrorMessage = "RequestDateTime is required")]
    public DateTime RequestDateTime { get; set; }
    [Required(ErrorMessage = "RequestHeaders is required")]
    public Dictionary<string, string> RequestHeaders { get; set; }
    [Required(ErrorMessage = "RequestHost is required")]
    public string RequestHost { get; set; }
    [Required(ErrorMessage = "RequestMethod is required")]
    public string RequestMethod { get; set; }
    [Required(ErrorMessage = "RequestPath is required")]
    public string RequestPath { get; set; }
     [Required(ErrorMessage = "RequestProtocol is required")]
    public string RequestProtocol { get; set; }
  [Required(ErrorMessage = "RequestBody is required")]
  [JsonPropertyName("responseBody")]
    public object ResponseBody { get; set; }
    [Required(ErrorMessage = "ResponseDateTime is required")]
    public DateTime ResponseDateTime { get; set; }
    [Required(ErrorMessage = "ServerName is required")]
    public string ServerName { get; set; }
    [Range(100, 599, ErrorMessage = "StatusCode must be 100-599")]
    public int StatusCode { get; set; }
    [Required(ErrorMessage = "User is required")]
    public string User { get; set; }
    [Required(ErrorMessage = "ElapsedMs is required")]
    public float ElapsedMs { get; set; }  

  }
}