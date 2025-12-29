using Tika.BatchIngestor.DemoApi.Configuration;
using Tika.BatchIngestor.Extensions.DependencyInjection;
using Tika.BatchIngestor.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
var databaseSettings = builder.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>() ?? new DatabaseSettings();

builder.Services.AddSingleton(databaseSettings);

// Add BatchIngestor factory for dynamic ingestor creation
builder.Services.AddBatchIngestorFactory();

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
        Description = "A demonstration API showcasing batch data ingestion capabilities for SQL Server and PostgreSQL using Tika.BatchIngestor.",
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

// Add startup banner
app.Logger.LogInformation("========================================");
app.Logger.LogInformation("  Tika.BatchIngestor Demo API Started");
app.Logger.LogInformation("========================================");
app.Logger.LogInformation("Swagger UI: {Url}", "https://localhost:5001");
app.Logger.LogInformation("SQL Server configured: {Configured}", !string.IsNullOrEmpty(databaseSettings.SqlServerConnectionString));
app.Logger.LogInformation("PostgreSQL configured: {Configured}", !string.IsNullOrEmpty(databaseSettings.PostgreSqlConnectionString));

app.Run();
