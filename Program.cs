using System.Data;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);


//Setup Serilog config
Log.Logger = new LoggerConfiguration()
.ReadFrom.Configuration(builder.Configuration)
.Enrich.FromLogContext()
.Enrich.WithMachineName()
.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning) // hides most ASP.NET noise
.MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
//console shows everything 
.WriteTo.Console(new JsonFormatter()) //default compact format that can look like the built-in ASP.NET output
//9200 is default HTTP port for elasticsearch
//Json sink
//Elasticsearch shows only accepted log
.WriteTo.Logger(lc => lc
    .Filter.ByExcluding(le => le.Properties.ContainsKey("Rejected") &&
                               le.Properties["Rejected"].ToString()=="True")
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
    AutoRegisterTemplate = true,
    IndexFormat = $"myapp-logs-{DateTime.UtcNow:yyyy-MM}"
    })
)
.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
.CreateLogger();

//remove built-in loggers
//[WARN] == buult-in asp.net console formatter
builder.Logging.ClearProviders();

builder.Host.UseSerilog();
//add serilog to Host
// builder.Host.UseSerilog((context, services, configuration) =>
// {
//     configuration
//         .MinimumLevel.Debug()
//         .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
// });


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

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
//serilog can log incoming HTTP requests w/o extra code
//capture incoming request paths, response statuses, and timing automatically
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();


app.Run();

