// Memorizer Self-Contained
// Based on the original Memorizer project by Dario Griffo and Petabridge, LLC
// Original: https://github.com/petabridge/memorizer
// Licensed under MIT License

using Memorizer.Self.Data;
using Memorizer.Self.Services;
using Memorizer.Self.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/memorizer-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Configuration
var modelsPath = builder.Configuration["ModelsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "models");
var dataPath = builder.Configuration["DataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
var dbPath = Path.Combine(dataPath, "memorizer.db");

var embeddingModelPath = Path.Combine(modelsPath, "all-MiniLM-L6-v2.gguf");
var llmModelPath = Path.Combine(modelsPath, "tinyllama-1.1b-chat.gguf");

// Ensure directories exist
Directory.CreateDirectory(dataPath);
Directory.CreateDirectory(modelsPath);

Log.Information("Starting Memorizer Self-Contained");
Log.Information("Models path: {ModelsPath}", modelsPath);
Log.Information("Data path: {DataPath}", dataPath);
Log.Information("Embedding model: {EmbeddingModel}", embeddingModelPath);
Log.Information("LLM model: {LLMModel}", llmModelPath);

// Services
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteVectorDb>>();
    return new SqliteVectorDb(dbPath, logger);
});

// Register services (using LLamaSharp 0.15.0 to avoid Microsoft.Extensions.AI conflicts)
builder.Services.AddSingleton<IEmbeddingService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LLamaEmbeddingService>>();
    return new LLamaEmbeddingService(embeddingModelPath, logger);
});

builder.Services.AddSingleton<ILLMService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LLamaService>>();
    return new LLamaService(llmModelPath, logger);
});

builder.Services.AddScoped<MemoryService>();

// MCP Server
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<MemoryTools>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Memorizer Self-Contained API",
        Version = "v2.0",
        Description = "Self-contained memory service with embedded vector database and LLM"
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("database", () =>
    {
        try
        {
            var db = builder.Services.BuildServiceProvider().GetRequiredService<SqliteVectorDb>();
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database is accessible");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database error", ex);
        }
    });

// CORS (for development)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapControllers();
app.MapMcp("/mcp"); // MCP endpoint at /mcp
app.MapHealthChecks("/healthz");

// Simple info endpoint
app.MapGet("/", () => new
{
    name = "Memorizer Self-Contained",
    version = "2.0.0",
    description = "Self-contained .NET memory service with embedded vector database and LLM",
    endpoints = new
    {
        api = "/api/memories",
        swagger = "/swagger",
        mcp = "/mcp",
        health = "/healthz"
    }
});

Log.Information("Memorizer Self-Contained started successfully");
Log.Information("API available at: http://localhost:8000/api/memories");
Log.Information("Swagger available at: http://localhost:8000/swagger");
Log.Information("MCP endpoint at: http://localhost:8000/mcp");

app.Run();
