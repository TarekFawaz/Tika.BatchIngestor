using Tika.BatchIngestor.Extensions.DependencyInjection;
using Tika.BatchIngestor.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add BatchIngestor factory with settings from configuration
// This reads from the "BatchIngestor" section in appsettings.json
builder.Services.AddBatchIngestorFactory(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks();

// Add controllers and API explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Tika.BatchIngestor Demo API",
        Version = "v1",
        Description = @"A demonstration API showcasing batch data ingestion capabilities using Tika.BatchIngestor.

## Supported Dialects
- **SqlServer** - Microsoft SQL Server
- **PostgreSql** - PostgreSQL / Generic
- **AuroraPostgreSql** - Amazon Aurora PostgreSQL
- **AuroraMySql** - Amazon Aurora MySQL
- **AzureSql** - Azure SQL Database

## Configuration
Configure connections in appsettings.json under the 'BatchIngestor' section.

## Usage
1. Use `/api/ingest/connections` to view configured connections
2. Use `/api/ingest/{connectionName}/sensors` to ingest data using a named connection
3. Use `/api/ingest/direct/sensors` to ingest with inline dialect and connection string",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Tika",
            Url = new Uri("https://github.com/TarekFawaz/Tika.BatchIngestor")
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tika.BatchIngestor Demo API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at the root
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Get settings for startup info
var settings = app.Services.GetRequiredService<BatchIngestorSettings>();
var enabledConnections = settings.Connections.Where(c => c.Enabled).ToList();

// Add startup banner
app.Logger.LogInformation("========================================");
app.Logger.LogInformation("  Tika.BatchIngestor Demo API Started");
app.Logger.LogInformation("========================================");
app.Logger.LogInformation("Swagger UI: https://localhost:5001");
app.Logger.LogInformation("Configured connections: {Count}", enabledConnections.Count);
foreach (var conn in enabledConnections)
{
    app.Logger.LogInformation("  - {Name} ({Dialect})", conn.Name, conn.Dialect);
}

app.Run();
