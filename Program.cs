using System.Data;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Identity;
using Nest;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;
using Serilog.Enrichers;

var builder = WebApplication.CreateBuilder(args);


//remove built-in loggers
builder.Logging.ClearProviders();

//register elasticsearch client as singleton
builder.Services.AddSingleton<IElasticClient>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    //use localhost if running outaside docker, "elasticsearch" if in same Docker network
    var esUrl = config["Elasticsearch:Url"] ?? "http://localhost:9200";
        var settings = new ConnectionSettings(new Uri(esUrl))
        .DefaultIndex("logs") // Optional default index
        .EnableDebugMode()    // Optional for debugging
        .EnableApiVersioningHeader(false)  // Disable strict version check
        .ServerCertificateValidationCallback((o, cert, chain, errors) => true) // Bypass SSL if needed
        .DisableDirectStreaming()  // For better debugging
        .PrettyJson()        // Optional for pretty JSON formatting
        .OnRequestCompleted(details => 
        {
            Console.WriteLine($"ES Request: {details.HttpMethod} {details.Uri}");
            Console.WriteLine($"ES Response: {details.HttpStatusCode}");
        });

    return new ElasticClient(settings);
}); 

//Setup Serilog config
Log.Logger = new LoggerConfiguration()
//.ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.WithProperty("Application", "LoggingAPI") //your service name
    .Enrich.FromLogContext()    //for dynamic properties, allow adding properties later
    .Enrich.WithMachineName()   //which server
    .Enrich.WithProcessId()     //add current process ID to every log entry
    .Enrich.WithThreadId()      //useful for debugging mulithreaded apps
                                //console shows everything 
    .WriteTo.Console() //default compact format that can look like the built-in ASP.NET output
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7, // Critical
        ModifyConnectionSettings = conn => conn
        .EnableDebugMode()
        .DisableDirectStreaming(),
    //FailureCallback = e => Console.WriteLine($"Elasticsearch failure: {e.MessageTemplate}")
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.MapGet("/", () =>
{
    //accepted log (from console + elasticsearch)
    Log.Information("Hello from Serilog direct to Elasticsearch!");
    return "OK";
});

app.MapGet("/reject", () =>
{
    //Rejected log(goes only to console)
    Log.ForContext("Rejected", true)
        .Warning("Rejected log due to validation failure!");
    return "Rejected";
});

//serilog can log incoming HTTP requests w/o extra code
//capture incoming request paths, response statuses, and timing automatically
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();


app.Run();

