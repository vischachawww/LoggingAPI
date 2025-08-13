

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;
using Nest;
using System.ComponentModel.DataAnnotations;
using Elasticsearch;
namespace LoggingAPI.Controllers
{

    //define base route 
    // API endpoint is POST http://localhost:<port>/logs
    [ApiController]
    [Route("logs")]
    public class LogsController : ControllerBase
    //define a controller named LogsController, inheriting from ControllerBase (used for APIs)
    {
        private readonly ILogger<LogsController> _logger;   //DI
        private readonly IElasticClient _elasticClient;
        public LogsController(ILogger<LogsController> logger, IElasticClient elasticClient)
        {
            _logger = logger;
            _elasticClient = elasticClient;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<LogEntry>>> PostLog([FromBody] LogEntry log)
        {
            try
            {
                // Validation
                if (!IsValidLog(log))
                {
                    _logger.LogWarning("Invalid log entry received: {@Log}", log);
                    return BadRequest(new ApiResponse<LogEntry>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = GetValidationErrors(log)
                    });
                }

                // Index to Elasticsearch
                var response = await _elasticClient.IndexDocumentAsync(log);
                if (!response.IsValid)
                {
                    _logger.LogError("Elasticsearch error: {DebugInfo}", response.DebugInformation);
                    return StatusCode(500, new ApiResponse<LogEntry>
                    {
                        Success = false,
                        Message = "Failed to index log",
                        Errors = new[] { response.OriginalException?.Message ?? "Unknown Elasticsearch error" }
                    });
                }

                return CreatedAtAction(nameof(PostLog), new ApiResponse<LogEntry>
                {
                    Success = true,
                    Message = "Log indexed successfully",
                    Data = log
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing log");
                return StatusCode(500, new ApiResponse<LogEntry>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new[] { ex.Message }
                });
            }
        }


        //validation method
        private bool IsValidLog(LogEntry log)
        {
            if (log == null) return false;

            var validLevels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };
            return log.Timestamp != default &&
            validLevels.Contains(log.Level.ToUpper()) &&
            !string.IsNullOrWhiteSpace(log.Source) &&
            !string.IsNullOrWhiteSpace(log.Message);
        }

        private IEnumerable<string> GetValidationErrors(LogEntry log)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(log.Message))
                errors.Add("Message is required");

            if (string.IsNullOrWhiteSpace(log.Level))
                errors.Add("Level is required");

            return errors;
        }

        //testing to make sure controller is working, temporary GET endpoint
        //shown in the web http://localhost:5191/logs
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "API is up!",
                Data = DateTime.UtcNow.ToString("o")
            });
        }

//health check endpoint
        [HttpGet("es-health")]
        public async Task<IActionResult> CheckElasticsearch()
        {
            try
            {
                // Basic ping check
                var pingResponse = await _elasticClient.PingAsync();
                if (!pingResponse.IsValid)
                    return StatusCode(500, "Failed to connect to Elasticsearch");

                // Simplified index check
                var indexResponse = await _elasticClient.Indices.ExistsAsync("logs-*");

                return Ok(new
                {
                    Status = "Operational",
                    PingSuccess = pingResponse.IsValid,
                    IndicesExist = indexResponse.Exists,
                    DebugInfo = pingResponse.DebugInformation
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
        


    }

}




