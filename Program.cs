using System.Data;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);


//Setup Serilog config
Log.Logger = new LoggerConfiguration()
.ReadFrom.Configuration(builder.Configuration)
.Enrich.FromLogContext()
.Enrich.WithMachineName()
.WriteTo.Console()
//why 9200 !!
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
{
    AutoRegisterTemplate = true,
    IndexFormat = $"myapp-logs-{DateTime.UtcNow:yyyy-MM}"
})
.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
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
    Log.Information("Hello form Serilog direct to Elasticsearch!");
    return "OK";
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

