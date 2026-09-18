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

# Open http://localhost:5000 in a browser for the dashboard: enter any public
# FPL team ID (find yours in fantasy.premierleague.com/entry/<id>/...) to see
# that squad, next-3-fixture difficulty per player, a suggested captain, a
# suggested starting XI/formation, and transfer suggestions. Click "Refresh
# player data" first if it's empty. Further down, enter a classic league ID
# to compare every manager in that league on squad value, bank, gameweek
# points and captain choice.

# Or drive the API directly:
curl -X POST http://localhost:5000/api/data/refresh

# List top-form midfielders
curl "http://localhost:5000/api/players?position=Midfielder&take=5"

# Ask a question (uses the offline generator unless Azure OpenAI is configured)
curl -X POST http://localhost:5000/api/advice \
  -H "Content-Type: application/json" \
  -d '{"question": "who should I captain this week?"}'

# Load a manager's live squad + suggestions (any public FPL team ID)
curl http://localhost:5000/api/team/153502

# Compare every manager in a classic league (find the ID in the league's URL:
# fantasy.premierleague.com/leagues/<id>/standings/c)
curl http://localhost:5000/api/league/275094

# Season-long gameweek-by-gameweek history (points, overall rank)
curl http://localhost:5000/api/team/153502/history
curl http://localhost:5000/api/league/275094/history
```

### The dashboard, and how the transfer/captain/lineup suggestions work

`GET /api/team/{teamId}` reads a manager's public squad straight from the
official FPL API (the same data shown on a manager's public profile page —
no login needed), joins it against the locally cached player data, and pulls
each player's next-3-fixture difficulty (FPL's own 1–5 FDR rating) from the
fixtures endpoint.

Every player then gets one blended, explainable score: recent form (weighted
highest — "who's good right now" is mostly a recency question), season-long
points, and fixture ease over the lookahead window. That score picks the
suggested captain (highest score in the starting XI), drives transfer
suggestions (the weakest starters, matched against same-position players not
already in the squad, priced within the actual sale price + money in the
bank, only ever suggested if the replacement scores strictly higher), and
picks a suggested starting XI: it brute-forces the 8 formations that are
legal in FPL (e.g. 3-4-3, 4-4-2, 4-5-1, 5-3-2...) against the manager's full
15, and returns whichever shape maximises total score, independent of any
transfers — so it's a separate answer to "who's good to own" (transfers) vs.
"who should I actually field this week" (lineup), with a plain-English list
of exactly which players to bench/start to get there. It's all heuristic and
intentionally simple rather than ML-based — same philosophy as the
`/api/advice` retrieval logic — which also makes it fast and fully unit
tested (`SquadAnalysisServiceTests`) with no external calls needed to test it.

### League comparison

`GET /api/league/{leagueId}` reads a classic league's public standings, then
fetches each manager's entry info and current picks (one call pair per team,
capped at 30 teams) to build a comparison table: rank and rank movement since
last gameweek, total and gameweek points, squad value, money in the bank, and
who they captained — plus three quick highlights (top scorer this gameweek,
most valuable squad, biggest climber). Aggregation is pure logic in
`LeagueAnalysisService`, unit tested the same way as the single-team
dashboard; a team whose data can't be loaded (private profile, transient
error) is silently left out rather than failing the whole comparison.

### Season history charts

`GET /api/team/{teamId}/history` and `GET /api/league/{leagueId}/history` read
the official FPL API's own per-manager history endpoint
(`entry/{teamId}/history/`) — the same gameweek-by-gameweek record shown on a
manager's public "History" tab — and the dashboard renders it as hand-rolled
SVG charts (no charting library, matching the rest of this frontend's
no-dependency scope): a bar chart of your own points per gameweek, a line
chart of your overall rank over the season, and, for a league, one line per
manager plotting cumulative season-total points side by side (with adaptive
y-axis gridlines that scale to the highest total in the league, e.g.
0/100/200/300), so you can see each manager's overall progress and spot the
gameweeks where lines cross as people swap places — each labelled with the
manager's name and — if they've set one — their favourite club's crest,
resolved via the official Premier League badge CDN
(`resources.premierleague.com/premierleague/badges/...`) from the club's
`code` field in `bootstrap-static` (kept in an in-memory cache only, not
persisted to the database, so adding it needed no schema change to the
existing SQLite file). A manager's crest is skipped gracefully — the chart
just shows their name — if they haven't set a favourite club or the crest
image fails to load.

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
- **`/api/advice` doesn't yet use fixture/gameweek context** — its answers are
  based on season stats only. The dashboard's transfer/captain suggestions do
  factor in upcoming fixture difficulty; wiring that same signal into the
  advice generator's retrieval step is a natural next step.
- **Current squad is read from the last *finished* gameweek's picks.**
  Squads carry over between gameweeks until a transfer is made, so this is
  accurate almost all the time — the exception is if you've already banked
  transfers for the next, not-yet-played gameweek, which won't show until it
  finishes.
- **Fixture lookahead counts fixtures, not distinct gameweeks** — a double
  gameweek (two matches in one gameweek) uses up two of the three preview
  slots for that team.
- **Transfer suggestions are single-player, same-position swaps** — they
  don't model chip strategy (Wildcard, Free Hit, Bench Boost) or multi-player
  moves, and don't check the "max 3 players from one club" squad rule.
- **The suggested lineup optimises total blended score, not expected points**
  — it doesn't know about the captain's points multiplier, so in rare cases
  a marginal formation change that "should" help can look like a wash.
- **League comparison is capped at 30 teams and one page of standings** — fine
  for a mini-league of friends, but a large public league would need
  pagination and a higher cap to show everyone.
- **History charts show gameweek points, not league-specific rank** — the FPL
  history endpoint gives overall rank (against every player in FPL, millions
  of people) and season totals, not a manager's rank *within one league* per
  gameweek. That would need to be derived by comparing every league member's
  cumulative totals gameweek-by-gameweek, which is a reasonable next step.
- **Crest badges depend on a third-party image CDN** — if Premier League ever
  changes that URL scheme, badges quietly stop loading (the chart still shows
  the manager's name either way, so nothing breaks, it just looks plainer).

## What this project is meant to demonstrate

- Consuming and modeling data from a real external API (C#, `HttpClient`,
  `System.Text.Json`) — including composing *three* separate public FPL
  endpoints (bootstrap, entry/picks, fixtures) into one coherent view
- A clean service-layer architecture with interfaces, dependency injection,
  and a graceful fallback pattern (Azure OpenAI ↔ offline mock)
- EF Core + SQLite for persistence, `IMemoryCache` for short-lived caching of
  third-party API responses that don't need a database
- A minimal API surface with clear, documented endpoints
- A small dependency-free HTML/CSS/JS frontend served as static files —
  proof the backend is usable end-to-end, not just via `curl`
- Unit testing business logic in isolation from infrastructure, including two
  scoring/recommendation engines (`SquadAnalysisService`, `LeagueAnalysisService`)
  with no external dependencies to mock
- Fanning out concurrent HTTP calls (`Task.WhenAll`) to compose a many-team
  league comparison from several single-team API calls, with per-team failures
  isolated so one bad team doesn't break the whole comparison
- CI via GitHub Actions
- A practical, from-scratch implementation of the retrieval-augmented
  generation pattern that Microsoft's AI-200 (Azure AI Cloud Developer
  Associate) certification is built around
