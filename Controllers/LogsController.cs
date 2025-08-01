

using Microsoft.AspNetCore.Mvc;
using LoggingAPI.Models;

namespace LoggingAPI.Controllers
{
    [ApiController]
    [Route("logs")]
    public class LogsCOntroller : ControllerBase
    {
        [HttpPost]
        public IActionResult Postlog([FromBody] LogEntry log)
        {
            if (!IsValidLog(log))
            {
                var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(errors => errors.ErrorMessage)
                .ToArray();
            }
                return BadRequest(new { error = "Invalid log format." });

            //later send to elastic search here

            return Created("", new { message = "Log accepted." });
        }

        private bool IsValidLog(LogEntry log)
        {
            var validLevels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };
            return log.Timestamp != default &&
            validLevels.Contains(log.Level.ToUpper()) &&
            !string.IsNullOrWhiteSpace(log.Service) &&
            !string.IsNullOrWhiteSpace(log.Message);
        }
    }
}

