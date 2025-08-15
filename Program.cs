using System.Data;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc; // For BadRequestObjectResult
using Nest;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;
using Serilog.Enrichers;
using Microsoft.AspNetCore.Diagnostics; //this provides IExceptionHandlerFeature

var builder = WebApplication.CreateBuilder(args);

//emove built-in loggers 
builder.Logging.ClearProviders();

//setup serilog config
// Log.Logger = new LoggerConfiguration()
//     //.MinimumLevel.Debug()
//     .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
//     .Enrich.WithProperty("Application", "LoggingAPI")   //your service name
//     .Enrich.FromLogContext()        //for dynamic properties, allow adding properties later
//     .Enrich.WithMachineName()       //which server
//     .Enrich.WithEnvironmentName()
//     .Enrich.WithProcessId()         //add current process ID to every log entry
//     .Enrich.WithThreadId()          //useful for debugging multithread apps
//     .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}")
//     .WriteTo.File("logs/log.txt", 
//         rollingInterval: RollingInterval.Day,
//         outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
//     .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
//     {
//         AutoRegisterTemplate = true,
//         IndexFormat = "logging-api-{0:yyyy.MM.dd}",
//         CustomFormatter = new ElasticsearchJsonFormatter()
//     })
//     .CreateLogger();

// 1️⃣ Read Serilog config from appsettings.json (and appsettings.{Environment}.json)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

//serilog registration, use serilog for all app logs = ONE only
builder.Host.UseSerilog();

//register elasticsearch client as singleton
//NEST's IElasticClient, push data to ES after pass thro logic in API
builder.Services.AddSingleton<IElasticClient>(provider => 
{
    var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
        .DefaultIndex("logs")   //optional default index
        .EnableApiVersioningHeader(false)   //disable strict version check
        .ServerCertificateValidationCallback((o, cert, chain, errors) => true)  
        .DisableDirectStreaming();  //for better debugging
    
    return new ElasticClient(settings);
});

//add services to the container
//builder.Services.AddControllers();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            
            Log.Warning("Malformed request: {@Errors}", errors);
            return new BadRequestObjectResult(new { Errors = errors });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    context.Request.Body.Position = 0;

    // Log.Debug("Request: {Method} {Path} {Body}",
    //     context.Request.Method,
    //     context.Request.Path,
    //     requestBody);

    await next();
});

//add Global Exception Handler to logs exceptions to Serilog and ES
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandler?.Error;

        // Log the full error
        Log.Error(exception, "Unhandled exception occurred");

        // Return a clean error response
        await context.Response.WriteAsJsonAsync(new
        {
            StatusCode = 500,
            Message = "An unexpected error occurred",
            Detailed = app.Environment.IsDevelopment() ? exception?.Message : null
        });
    });
});


// HTTP logging middleware
//logs every request method, path, status code and duration in console
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    await next();
    var elapsed = DateTime.UtcNow - start;
    
    Log.Information("{Method} {Path} responded {StatusCode} in {Elapsed}ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        elapsed.TotalMilliseconds);
});



app.UseEndpoints(endpoints => endpoints.MapControllers());

try
// {
//     Log.Information("Application starting up");
//     app.Run();
// }
{ //NEW
    Log.Information("Application started");

    // Simulating request logging with LogContext
    using (LogContext.PushProperty("RequestId", Guid.NewGuid()))
    using (LogContext.PushProperty("Requester", "JohnDoe"))
    using (LogContext.PushProperty("ContactId", "AUTO-8d3a-86c825"))
    using (LogContext.PushProperty("Environment", "Production"))
    using (LogContext.PushProperty("SourceContext", "Health.Preadmission.Web.PreadmissionController"))
    {
        Log.Information("Failed to log on user {ContactId}", "AUTO-8d3a-86c825");
    }

    Log.Information("Application finished");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

