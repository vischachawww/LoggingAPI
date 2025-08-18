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
using Microsoft.AspNetCore.Diagnostics; //this provides IExceptionHandlerFeature
using LoggingAPI.Models;

var builder = WebApplication.CreateBuilder(args);

//emove built-in loggers 
builder.Logging.ClearProviders();

// read Serilog config from appsettings.json 
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

//serilog registration, use serilog for all app logs 
builder.Host.UseSerilog();

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
        .DefaultMappingFor<LogEntry>(m => m
            .IdProperty(e => e.RequestId) // Use RequestId as ES document ID
        );
    
    return new ElasticClient(settings);
});

//add services to the container
//builder.Services.AddControllers();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true; //we handle verification manually
    });


builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c => 
{
    c.SwaggerDoc("v1", new() { Title = "Bank Logging API", Version = "v1" });
});

var app = builder.Build();

//middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bank Logging API v1");
    });
}

app.UseRouting();


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

// Centralized error handling
app.UseExceptionHandler("/error");

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

