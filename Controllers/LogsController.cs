

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;
using Nest;

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
        public LogsController(ILogger<LogsController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Postlog([FromBody] LogEntry log)
        {
            //custom check to make sure all field is valid
            if (!IsValidLog(log))
            {
                _logger.LogWarning("Invalid log entry received: {@Log}", log);
                var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(errors => errors.ErrorMessage)
                .ToArray();

                return BadRequest(new ApiResponse  //400
                {
                    Message = "Validation failed",
                    Error = "Invalid log format.",
                    Data = errors
                });
            }

            //simulate different behavior by log level 
            //to differentiate each log is which category and print extra details

            switch (log.Level?.ToUpper())
            {
                case "INFO":
                    Console.WriteLine($"[INFO] {log.Timestamp}: {log.Message}");
                    break;
                case "WARN":
                    Console.WriteLine($"[WARN] {log.Timestamp}: {log.Message}");
                    break;
                case "ERROR":
                    Console.WriteLine($"[ERROR] {log.Timestamp}: {log.Message}\nCode: {log.ErrorCode}\nStack: {log.StackTrace}");
                    break;
                case "DEBUG":
                    Console.WriteLine($"[DEBUG] {log.Timestamp}: {log.Message}");
                    break;
            }
            //later send to elastic search here
            // Save to Elasticsearch
            var client = ElasticClientProvider.GetClient();
            var response = client.IndexDocument(log);

            if (!response.IsValid)
            {
                _logger.LogError("Failed to index log in ELasticsearch: {Error}", response.OriginalException.Message);
                return StatusCode(500, new ApiResponse
                {
                    Message = "Log indexing failed.",
                    Error = response.OriginalException.Message
                });
            }

            return Created("", new ApiResponse    //201
            {
                Message = "Log accepted and saved to Elasticsearch.",
                Data = log
            });

        }

        //validation method
        private bool IsValidLog(LogEntry log)
        {
            var validLevels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };
            return log.Timestamp != default &&
            validLevels.Contains(log.Level.ToUpper()) &&
            !string.IsNullOrWhiteSpace(log.Source) &&
            !string.IsNullOrWhiteSpace(log.Message) &&
            !string.IsNullOrWhiteSpace(log.UserID) &&
            !string.IsNullOrWhiteSpace(log.RequestPath);
        }

        //testing to make sure controller is working, temporary GET endpoint
        //shown in the web http://localhost:5191/logs
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("API is up!");
        }
    }


}




