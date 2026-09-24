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

// --- Team dashboard: a manager's live squad, next-fixture difficulty, and
//     transfer/captain suggestions. Pulls from the same public FPL API as the
//     data ingestion above, just different endpoints (entry, picks, fixtures). ---
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IFplGameweekService, FplGameweekService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FplAiAssistant/1.0 (portfolio project)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IFplFixtureService, FplFixtureService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FplAiAssistant/1.0 (portfolio project)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IFplTeamService, FplTeamService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FplAiAssistant/1.0 (portfolio project)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ISquadAnalysisService, SquadAnalysisService>();

// --- League comparison: standings for a classic mini-league plus the same
//     squad-value/bank/captain signals as the single-team dashboard, so a
//     group of friends can see not just who's ahead but why. ---
builder.Services.AddHttpClient<IFplLeagueService, FplLeagueService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FplAiAssistant/1.0 (portfolio project)");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ILeagueAnalysisService, LeagueAnalysisService>();

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

// --- Web dashboard (wwwroot/index.html + app.js) — a small static UI on top
//     of the API below. No build step: plain HTML/CSS/JS, fetch()ed straight
//     from the endpoints this file maps. ---
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    adviceGenerator = azureOpenAiConfigured ? "azure-openai" : "offline-mock",
}))
.WithName("Health");

app.MapGroup("/api/data").MapDataEndpoints();
app.MapGroup("/api/players").MapPlayerEndpoints();
app.MapGroup("/api/advice").MapAdviceEndpoints();
app.MapGroup("/api/team").MapTeamEndpoints();
app.MapGroup("/api/league").MapLeagueEndpoints();

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
