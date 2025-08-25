

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;
using Nest;
using Elasticsearch;
using Serilog.Context;
using Serilog;
using Serilog.Enrichers;
using System.ComponentModel.DataAnnotations;

namespace LoggingAPI.Controllers

{
    //define base route 
    // API endpoint is POST http://localhost:<port>/logs 
    [ApiController]
    [Route("logs")]
    [Produces("application/json")]
    public class LogsController : ControllerBase
    //define a controller named LogsController, inheriting from ControllerBase (used for APIs)
    {
        private readonly ILogger<LogsController> _logger;   //Microsoft.Extensions.Logging
        private readonly IElasticClient _elasticClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public LogsController(ILogger<LogsController> logger, IElasticClient elasticClient, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _elasticClient = elasticClient;
            _httpContextAccessor = httpContextAccessor;
        }

        //REST API Controller
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<LogEntry>>> PostLog([FromBody] LogEntry log)
        {
            // Log any incoming request
            //_logger.LogInformation("Received log submission: {@LogEntry}", log);

            // Auto-populate server-generated fields
            var httpContext = _httpContextAccessor.HttpContext;
            log.CorrelationId ??= httpContext?.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
            log.ServerName ??= Environment.MachineName;
            log.RemoteServerIp ??= httpContext?.Connection.RemoteIpAddress?.ToString();
            log.User ??= httpContext?.User.Identity?.Name ?? "Anonymous";

            // Strict validation 
            var validationErrors = GetValidationErrors(log);
            if (validationErrors.Any())
            {
                _logger.LogWarning("Invalid log entry: {Errors}", string.Join(", ", validationErrors));
                return BadRequest(new ApiResponse<LogEntry>     //swagger sees 400
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = validationErrors
                });
            }
            try
            {
                // Structured logging context
                using (LogContext.PushProperty("LogEntry", log, true))
                {
                    Log.Information("API log received");
                }

                // Elasticsearch indexing
                //API sends log to ES using NEST client
                var response = await _elasticClient.IndexDocumentAsync(log);  //index single docuemnt
                if (!response.IsValid)
                {
                    throw new Exception(response.DebugInformation);
                }

                return Ok(new ApiResponse<LogEntry>     //swagger sees 200
                {
                    Success = true,
                    Message = "Log indexed successfully",
                    Data = log
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log processing failed");
                return StatusCode(500, new ApiResponse<LogEntry>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new[] { ex.Message }
                });
            }

        }

        private List<string> GetValidationErrors(LogEntry log)
        {
            var errors = new List<string>();

            // Manual validation (beyond DataAnnotations)
            if (log == null)
            {
                errors.Add("Log entry cannot be null");
                return errors;
            }

            // Validate DateTime sequence
            if (log.ResponseDateTime < log.RequestDateTime)
            {
                errors.Add("ResponseDateTime cannot be earlier than RequestDateTime");
            }

            // Validate headers
            if (log.RequestHeaders == null || !log.RequestHeaders.Any())
            {
                errors.Add("At least one request header is required");
            }
            // Add DataAnnotations validation errors
            var validationContext = new ValidationContext(log);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(log, validationContext, validationResults, true);
            errors.AddRange(validationResults.Select(v => v.ErrorMessage));

            return errors;
        }

        //testing to make sure controller is working, temporary GET endpoint
        //shown in the http://localhost:5191/logs
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


        [HttpGet("test")]
        public IActionResult Test()
        {
            Log.Information("This is a test message");
            return Ok();
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
                var indexResponse = await _elasticClient.Indices.ExistsAsync("loggingAPI-*");

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




