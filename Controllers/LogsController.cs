

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;

namespace LoggingAPI.Controllers
{

    //define base route 
    // API endpoint is POST http://localhost:<port>/logs
    [ApiController]
    [Route("logs")]
    public class LogsController : ControllerBase
    //define a controller named LogsController, inheriting from ControllerBase (used for APIs)
    {
        private readonly ILogger<LogsController> _logger;
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

                return BadRequest(new ApiResponse
                {
                    Message = "Validation failed",
                    Error = "Invalid log format.",
                    Data = errors
                });
            }

            //simulate different behavior by log level 
            //to differentiate each log is which category and print extra details
            //basically prints back the full log
            // !!
            switch (log.Level.ToUpper())
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

            _logger.LogInformation("Log entry validated succesfully: {@Log}", log);
            return Created("",new ApiResponse
            {
                Message = "Log accepted.",
                Data = log
            });

        }

        //validation method
        private bool IsValidLog(LogEntry log)
        {
            var validLevels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };
            return log.Timestamp != default &&
            validLevels.Contains(log.Level.ToUpper()) &&
            !string.IsNullOrWhiteSpace(log.Service) &&
            !string.IsNullOrWhiteSpace(log.Message);
        }

        //testing to make sure controller is working, temporary GET endpoint
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("API is up!");
        }
    }
    
    
    
    
    


}

