// LoggingMiddleware.cs
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        //start with basic request info (NO AUTH DETAILS)
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method,
            ["TraceId"] = context.TraceIdentifier
        }))
        {
            try
            {
                await _next(context);
                
                _logger.LogInformation("Request completed: {StatusCode}", 
                    context.Response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Request failed");
                throw;
            }
        }
    }
}