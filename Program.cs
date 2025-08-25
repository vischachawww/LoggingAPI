//Application configuration and startup
using System.Data;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc; // For BadRequestObjectResult
using Nest;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Context;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;
using Serilog.Enrichers;
using Microsoft.OpenApi.Models; 
using Microsoft.AspNetCore.Diagnostics; //this provides IExceptionHandlerFeature
using LoggingAPI.Models;

var builder = WebApplication.CreateBuilder(args);

//emove built-in loggers 
builder.Logging.ClearProviders();

// read Serilog config from appsettings.json 
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

//required for accessing HTTP context in controller
builder.Services.AddHttpContextAccessor();

//serilog registration, use serilog for all app logs 
//builder.Host.UseSerilog();
// Enhanced Serilog configuration
builder.Host.UseSerilog((ctx, config) => 
{
    config.ReadFrom.Configuration(ctx.Configuration)
          //add fixed properties to every log
          .Enrich.WithProperty("Application", "LoggingAPI")
          .Enrich.WithProperty("@timestamp", DateTime.UtcNow)
          .Enrich.FromLogContext();
});

//register elasticsearch client as singleton
//NEST's IElasticClient, push data to ES after pass thro logic in API
builder.Services.AddSingleton<IElasticClient>(provider => 
{
    var indexName = $"logging-api-{DateTime.UtcNow:yyyy.MM.dd}";
    var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
       .DefaultIndex(indexName)
        .EnableApiVersioningHeader(false)   //disable strict version check
        .ServerCertificateValidationCallback((o, cert, chain, errors) => true)
        .DisableDirectStreaming()  //for better debugging
        .DefaultMappingFor<LogEntry>(m => m   //ES default behavior with documentID, updates/? **
            .IdProperty(e => e.CorrelationId)
            .IndexName(indexName)
        )
        .OnRequestCompleted(details => 
        {
            Log.Information("ES Request: {Method} {Path} {Status}", 
                details.HttpMethod, 
                details.Uri, 
                details.HttpStatusCode);
        });
    
    return new ElasticClient(settings);
});

//add services to the container
//builder.Services.AddControllers();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true; //we handle verification manually
    })
     .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = null; // Keeps PascalCase
    });

builder.Services.AddEndpointsApiExplorer();

//Swagger
builder.Services.AddSwaggerGen(c => 
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
    Title = "Logging API", 
    Version = "v1",
    Description = "Centralized logging system for banking applications"
    });
});

var app = builder.Build();

//middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Logging API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();


// HTTP logging middleware
//logs every request method, path, status code and duration in console
app.Use(async (context, next) =>
{
    //start timer
    var startTime = DateTime.UtcNow;
    var CorrelationId = Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = CorrelationId;

    //request body capture
    context.Request.EnableBuffering(); //allow multiple reads od the requests stream
    var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
    context.Request.Body.Position = 0; //rewind for actual processing

    //store response for later logging
    var originalResponseBody = context.Response.Body;
    using var responseBodyBuffer = new MemoryStream();
    context.Response.Body = responseBodyBuffer;

    await next(); //continue to controller

    //response body capture
    responseBodyBuffer.Seek(0, SeekOrigin.Begin);
    var responseBody = await new StreamReader(responseBodyBuffer).ReadToEndAsync();
    responseBodyBuffer.Seek(0, SeekOrigin.Begin);

    //enhanced logging (now with bodies)
    var elapsed = DateTime.UtcNow - startTime;

    Log.ForContext("@timestamp", DateTime.UtcNow)
        .ForContext("CorrelationId", CorrelationId)
        .ForContext("RemoteServerIp", context.Connection.RemoteIpAddress)
        .ForContext("RequestBody", requestBody)
        .ForContext("RequestDateTime", startTime)
        .ForContext("RequestHeaders", context.Request.Headers)
        .ForContext("RequestHost", context.Request.Host)
        .ForContext("RequestMethod", context.Request.Method)
        .ForContext("RequestPath", context.Request.Path)
        .ForContext("RequestProtocol", context.Request.Protocol)
        .ForContext("ResponseBody", responseBody)
        .ForContext("ResponseDateTime", DateTime.UtcNow)
        .ForContext("ServerName", Environment.MachineName)
        .ForContext("StatusCode", context.Response.StatusCode)
        .ForContext("User", context.User.Identity?.Name ?? "Anonymous")
        .ForContext("Elapsed", elapsed)
        .Information("API {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed}ms", 
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);

    await responseBodyBuffer.CopyToAsync(originalResponseBody);
});

// Centralized error handling
//app.UseExceptionHandler("/error");
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandler?.Error;

        Log.Error(exception, "Unhandled exception occurred");
        
        await context.Response.WriteAsJsonAsync(new
        {
            StatusCode = 500,
            Message = "An error occurred while processing your request",
            CorrelationId = context.Items["CorrelationId"]?.ToString()
        });
    });
});

// Health check endpoint
//app.MapHealthChecks("/health");

app.MapControllers();

app.UseEndpoints(endpoints => endpoints.MapControllers());

try
{ 
    Log.Information("Starting Bank Logging API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

