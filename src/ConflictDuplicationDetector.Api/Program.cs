using ConflictDuplicationDetector.Api.Endpoints;
using ConflictDuplicationDetector.Api.Middleware;
using ConflictDuplicationDetector.Api.Services;
using ConflictDuplicationDetector.Api.Swagger;
using ConflictDuplicationDetector.Core.DependencyInjection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddConflictDuplicationDetector(builder.Configuration);
builder.Services.AddSingleton<DetectorApplicationService>();
builder.Services.AddSingleton<IJobService, JobService>();
builder.Services.AddHostedService<BackgroundJobProcessor>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Conflict & Duplication Detector API",
        Version = "v1",
        Description = """
            HTTP API for document ingestion, knowledge-base analysis, file checks, and chat.

            Long-running operations (ingest, analyse, check, chat) return **202 Accepted** with a job ID.
            Poll **GET /api/jobs/{jobId}** until status is `completed` or `failed`.
            """
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    options.OperationFilter<SwaggerExamplesFilter>();

    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "API key passed in the X-Api-Key header."
    };
    options.AddSecurityDefinition("ApiKey", apiKeyScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            []
        }
    });
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Conflict Duplication Detector API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapRazorPages();
app.MapDetectorApi();

var uploadsPath = builder.Configuration["Storage:UploadsPath"] ?? "./data/uploads";
Directory.CreateDirectory(uploadsPath);
var dataDir = Path.GetDirectoryName(builder.Configuration["VectorStore:PersistPath"] ?? "./data/vectors.json");
if (!string.IsNullOrEmpty(dataDir))
    Directory.CreateDirectory(dataDir);

app.Run();
