//HTTP Traffic Logger, logs every HTTP req/res
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Diagnostics;
using System.Text;

namespace LoggingAPI.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var originalBodyStream = context.Response.Body;

            try
            {
                // Log request
                var request = await FormatRequest(context.Request);
                _logger.LogInformation("Incoming Request: {Method} {Path} {QueryString}\nHeaders: {Headers}\nBody: {RequestBody}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString,
                    context.Request.Headers,
                    request);
                // Copy response stream for logging
                using (var responseBody = new MemoryStream())
                {
                    context.Response.Body = responseBody;

                    await _next(context);

                    // Log response
                    var response = await FormatResponse(context.Response);
                    _logger.LogInformation("Outgoing Response: {StatusCode} in {ElapsedMilliseconds}ms\nHeaders: {Headers}\nBody: {ResponseBody}",
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        context.Response.Headers,
                        response);

                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in request pipeline");
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
                stopwatch.Stop();
            }
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            request.EnableBuffering();
            var body = request.Body;
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            request.Body.Position = 0;
            return Encoding.UTF8.GetString(buffer);
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text;
        }
    }
}