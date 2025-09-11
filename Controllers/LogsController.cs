

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;
using Nest;
using Elasticsearch;
using Serilog.Context;
using Serilog;
using Serilog.Enrichers;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace LoggingAPI.Controllers

{
    //define base route 
    // API endpoint is POST http://localhost:<port>/logs 
    [ApiController]
    [Route("logs")]
    [Produces("application/json")]
    [Authorize]
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
        [Authorize]
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
            //extracxt appName from JWT claims
            var appNameFromToken = User.Claims.FirstOrDefault(c => c.Type == "ApplicationName")?.Value;
            if (string.IsNullOrEmpty(appNameFromToken))
            {
                return Unauthorized(new ApiResponse<LogEntry>
                {
                    Success = false,
                    Message = "No application identity in token"
                });
            }

            // enforce that log.ApplicationName must match token's ApplicationName
            if (!string.Equals(log.ApplicationName, appNameFromToken, StringComparison.OrdinalIgnoreCase))
            {
                return new ObjectResult(new ApiResponse<LogEntry>
                {
                    Success = false,
                    Message = $"Application '{log.ApplicationName}' is not authorized to send logs with this token."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

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

        //shown in the http://localhost:5191/logs
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(List<LogEntry>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLogs()
        {
            try
            {
                // Simple search - get recent logs
                var response = await _elasticClient.SearchAsync<LogEntry>(s => s
                    .Sort(so => so.Descending(f => f.RequestDateTime))
                    .Size(10));

                //return Ok(response.Documents);
                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = "API is up! This is a protected endpoint",
                    Data = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Elasticsearch error",
                    Detail = ex.Message
                });
            }
        }

        //health check endpoint
        [HttpGet("health")]
        [ProducesResponseType(typeof(ElasticHealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                // Check Elasticsearch connection
                var health = await _elasticClient.Cluster.HealthAsync();
                var response = new ElasticHealthResponse
                {
                    Status = health.Status.ToString(),
                    StatusDescription = health.Status.ToString() switch
                    {
                        "Green" => "Fully operational - all shards allocated",
                        "Yellow" => "Functional but some replicas missing - expected in single node",
                        "Red" => "Critical failure - primary shards unavailable",
                        _ => "Unknown status"
                    },
                    NodeCount = health.NumberOfNodes,
                    IsHealthy = health.Status == Elasticsearch.Net.Health.Green,  // Fixed namespace
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(503, new ProblemDetails
                {
                    Title = "Elasticsearch unavailable",
                    Detail = ex.Message,
                    Status = 503
                });
            }
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchLogs(
            [FromQuery] string query = "",
            [FromQuery] string applicationName = null,
            [FromQuery] string last = null,  // 1h, 2d
            [FromQuery] int size = 100)
        {
            var mustQueries = new List<Func<QueryContainerDescriptor<LogEntry>, QueryContainer>>();
            if (!string.IsNullOrEmpty(applicationName))
            {
                mustQueries.Add(m => m.Term(t => t.ApplicationName, applicationName));
            }

            if (!string.IsNullOrEmpty(query))
            {
                mustQueries.Add(m => m.QueryString(qs => qs.Query(query)));
            }
            if (!string.IsNullOrEmpty(last))
            {
                var now = DateTime.UtcNow;
                if (last.EndsWith("d") && int.TryParse(last.TrimEnd('d'), out int days))
                {
                    mustQueries.Add(m => m.DateRange(dr => dr
                        .Field(f => f.RequestDateTime)
                        .GreaterThanOrEquals(now.AddDays(-days))
                        .LessThanOrEquals(now)
                    ));
                }
                else if (last.EndsWith("h") && int.TryParse(last.TrimEnd('h'), out int hours))
                {
                    mustQueries.Add(m => m.DateRange(dr => dr
                        .Field(f => f.RequestDateTime)
                        .GreaterThanOrEquals(now.AddHours(-hours))
                        .LessThanOrEquals(now)
                    ));
                }
                else if (last.EndsWith("m") && int.TryParse(last.TrimEnd('m'), out int minutes))
                {
                    mustQueries.Add(m => m.DateRange(dr => dr
                        .Field(f => f.RequestDateTime)
                        .GreaterThanOrEquals(now.AddMinutes(-minutes))
                        .LessThanOrEquals(now)
                    ));
                }
            }

            //Build Elasticsearch query
            var searchResponse = await _elasticClient.SearchAsync<LogEntry>(s => s
                .Query(q => q.Bool(b => b.Must(mustQueries.ToArray())))
                .Sort(so => so.Descending(f => f.RequestDateTime))
                .Size(size)
            );

            return Ok(searchResponse.Documents);
        }


        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                // total logs count in ES
                var totalLogs = await _elasticClient.CountAsync<LogEntry>();

                // Count validation errors (400-499)
                var validationErrors = await _elasticClient.CountAsync<LogEntry>(c => c
                    .Query(q => q
                        .Range(r => r
                            .Field(f => f.StatusCode)
                            .GreaterThanOrEquals(400)
                            .LessThan(500)
                        )
                    )
                );

                // Count server errors (500-599)
                var serverErrors = await _elasticClient.CountAsync<LogEntry>(c => c
                    .Query(q => q
                        .Range(r => r
                            .Field(f => f.StatusCode)
                            .GreaterThanOrEquals(500)
                        )
                    )
                );

                // Calculate rates
                double validationErrorRate = totalLogs.Count > 0
                    ? (validationErrors.Count * 100.0) / totalLogs.Count
                    : 0;

                double serverErrorRate = totalLogs.Count > 0
                    ? (serverErrors.Count * 100.0) / totalLogs.Count
                    : 0;

                double totalErrorRate = validationErrorRate + serverErrorRate;

                //get most active user = user with most log entries
                var userStats = await _elasticClient.SearchAsync<LogEntry>(s => s
                    .Size(0)
                    .Aggregations(aggs => aggs
                        .Terms("active_users", t => t
                            .Field(f => f.User.Suffix("keyword"))
                            .Size(1)
                        )
                    )
                );

                var mostActiveUser = userStats.Aggregations.Terms("active_users")?.Buckets?.FirstOrDefault()?.Key ?? "N/A";

                return Ok(new ApiStatsResponse
                {
                    TotalLogs = totalLogs.Count,
                    ValidationErrorRate = Math.Round(validationErrorRate, 2),
                    ServerErrorRate = Math.Round(serverErrorRate, 2),
                    TotalErrorRate = Math.Round(totalErrorRate, 2),
                    MostActiveUser = mostActiveUser.ToString(),
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Failed to retrieve statistics",
                    Detail = ex.Message
                });
            }
        }
    }


}

public class ApiStatsResponse
{
    public long TotalLogs { get; set; }
    //adding multiple metrics
     public double ValidationErrorRate { get; set; }  // 400-499
    public double ServerErrorRate { get; set; }      // 500-599
    public double TotalErrorRate { get; set; }       // 400-599
    public string MostActiveUser { get; set; }
    public DateTime Timestamp { get; set; }
}


