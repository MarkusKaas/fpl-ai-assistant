# FPL AI Assistant

A small backend service that pulls live Fantasy Premier League data and answers
natural-language questions about it ("who should I captain this week?", "give
me a cheap differential defender") — grounded in real, current stats rather
than a language model's guesswork.

Built as a portfolio project to pair real backend engineering (C#/.NET, EF
Core, a public API integration) with the AI-integration pattern that's
increasingly expected of "backend developer" roles in 2026: retrieval-augmented
generation (RAG), where you fetch real data first and only then hand it to a
language model to explain in plain English — instead of asking the model to
recall facts from memory.

## Why this exists

I'm a backend/software engineering student finishing my Diplomingeniør in
January 2027, currently doing a backend internship in C#/Umbraco/Azure. I
already had a small personal script that scraped FPL data for my own team
picks — this project turns that into something closer to a real service: a
proper API, a database, tests, CI, and an actual AI integration on top,
rather than a one-off script.

## Architecture

```
                    ┌─────────────────────────────┐
                    │   Official FPL public API    │
                    │  (bootstrap-static endpoint) │
                    └──────────────┬───────────────┘
                                   │  HTTP GET (no auth needed)
                                   ▼
                    ┌─────────────────────────────┐
                    │      FplDataService          │  POST /api/data/refresh
                    │   (ingest + flatten + save)  │
                    └──────────────┬───────────────┘
                                   ▼
                    ┌─────────────────────────────┐
                    │   SQLite DB (via EF Core)    │  GET /api/players
                    └──────────────┬───────────────┘
                                   │
                                   ▼
                    ┌─────────────────────────────┐
                    │  PlayerRetrievalService      │  "R" in RAG — pure,
                    │  (keyword + form/points      │  unit-tested logic,
                    │   scoring, no external call) │  no external deps
                    └──────────────┬───────────────┘
                                   ▼
                    ┌─────────────────────────────┐
                    │  IAdviceGenerator            │  "G" in RAG
                    │  ┌─────────────────────────┐ │  POST /api/advice
                    │  │ AzureOpenAiAdviceGen.    │ │
                    │  │ (used when configured)   │ │
                    │  ├─────────────────────────┤ │
                    │  │ MockAdviceGenerator      │ │
                    │  │ (free offline fallback)  │ │
                    │  └─────────────────────────┘ │
                    └─────────────────────────────┘
```

The advice generator is picked at startup based on whether Azure OpenAI
credentials are configured. With none set, the app still runs completely —
`/api/advice` falls back to a deterministic, free offline generator that
formats the same retrieved data as a template answer. This means anyone
cloning the repo (including GitHub Actions CI) can build, test, and run the
whole thing with zero secrets and zero cost, and the answers only get
noticeably smarter, not functionally different, once a real key is added.

## Running it

```bash
# Restore & build
dotnet restore
dotnet build

# Run the API (SQLite database file is created automatically on first run)
dotnet run --project src/FplAiAssistant.Api

# In another terminal: load the current season's player data
curl -X POST http://localhost:5000/api/data/refresh

# List top-form midfielders
curl "http://localhost:5000/api/players?position=Midfielder&take=5"

# Ask a question (uses the offline generator unless Azure OpenAI is configured)
curl -X POST http://localhost:5000/api/advice \
  -H "Content-Type: application/json" \
  -d '{"question": "who should I captain this week?"}'
```

### Enabling real AI-generated answers

The app needs three settings to switch on the Azure OpenAI generator. Don't
put real values in `appsettings.json` — use `dotnet user-secrets` locally, or
environment variables / App Service configuration when deployed:

```bash
cd src/FplAiAssistant.Api
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-key>"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "<your-deployment-name>"
```

## Tests

```bash
dotnet test
```

The test suite covers `PlayerRetrievalService` — the retrieval/scoring logic
that decides which players get shown to the model. It's deliberately isolated
from the database, the FPL API, and Azure OpenAI, so the tests run instantly
and need no network access or credentials — which is also why they're safe to
run in CI on every push (see `.github/workflows/ci.yml`).

## Known limitations / what I'd do next

Being upfront about the corners cut to keep this a weekend-sized project:

- **Data refresh is full-replace, not upsert.** Fine at ~700 players, but a
  real version would diff and update changed rows only.
- **Retrieval is heuristic, not embeddings-based.** It's simple keyword and
  score-based filtering, not a vector search. That's a deliberate choice —
  it's fast, free, fully explainable, and good enough for this dataset size —
  but a natural next step would be embedding player data and using a proper
  vector similarity search, which is also on the AI-200 exam syllabus.
- **No auth, no rate limiting.** Fine for a local/portfolio demo, not for a
  public deployment.
- **No fixtures/gameweek context yet** — advice is based on season stats
  only, not upcoming opponent difficulty. That's the most useful next feature.

## What this project is meant to demonstrate

- Consuming and modeling data from a real external API (C#, `HttpClient`,
  `System.Text.Json`)
- A clean service-layer architecture with interfaces, dependency injection,
  and a graceful fallback pattern (Azure OpenAI ↔ offline mock)
- EF Core + SQLite for persistence
- A minimal API surface with clear, documented endpoints
- Unit testing business logic in isolation from infrastructure
- CI via GitHub Actions
- A practical, from-scratch implementation of the retrieval-augmented
  generation pattern that Microsoft's AI-200 (Azure AI Cloud Developer
  Associate) certification is built around
