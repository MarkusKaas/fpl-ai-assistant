using FplAiAssistant.Api.Data;
using FplAiAssistant.Api.Endpoints;
using FplAiAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database (SQLite — zero setup, fine for a single-instance MVP) ---
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=fplai.db";
builder.Services.AddDbContext<FplDbContext>(options => options.UseSqlite(connectionString));

// --- Data ingestion (typed HttpClient calling the public FPL API) ---
builder.Services.AddHttpClient<IFplDataService, FplDataService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FplAiAssistant/1.0 (portfolio project)");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Retrieval (pure logic, no external dependencies) ---
builder.Services.AddScoped<IPlayerRetrievalService, PlayerRetrievalService>();

// --- Advice generation: use Azure OpenAI only if it's actually configured,
//     otherwise fall back to the free offline generator. This means the app
//     (and CI) always runs end-to-end with zero setup, and upgrades to real
//     AI-generated answers the moment credentials are supplied. ---
var azureOpenAiConfigured =
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureOpenAI:Endpoint"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureOpenAI:ApiKey"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureOpenAI:DeploymentName"]);

if (azureOpenAiConfigured)
{
    builder.Services.AddSingleton<IAdviceGenerator, AzureOpenAiAdviceGenerator>();
}
else
{
    builder.Services.AddSingleton<IAdviceGenerator, MockAdviceGenerator>();
}

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    adviceGenerator = azureOpenAiConfigured ? "azure-openai" : "offline-mock",
}))
.WithName("Health");

app.MapGroup("/api/data").MapDataEndpoints();
app.MapGroup("/api/players").MapPlayerEndpoints();
app.MapGroup("/api/advice").MapAdviceEndpoints();

// Ensure the SQLite schema exists. A real production app would use EF Core
// migrations (`dotnet ef migrations add ...`) instead — EnsureCreated is the
// pragmatic choice for a single-file MVP database with no schema history to manage yet.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FplDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests, if added later.
public partial class Program { }
