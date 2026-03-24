# MIS 321 — Team Project 2 (Mulholland Real Estate)

Property-management maintenance **triage** demo: ASP.NET Core 9 Web API (EF Core + **MySQL** via Pomelo), Bootstrap + vanilla JS client, and **n8n webhook** triage with **heuristic fallback**. Deployed DB can live on **Heroku** (see below). Sample data: `Sample Data/Maintenance Report Example.xlsx` → `Data/maintenance-seed.json`.

## Repo layout

| Path | Purpose |
|------|---------|
| `API/MulhollandRealEstate.API/` | Web API, migrations, seed JSON |
| `Client/` | Static dashboard (`index.html`, `app.js`, `styles.css`) |
| `Sample Data/` | Source Excel for seeded tickets |
| `IMPLEMENTATION_TASKS.md` | Living checklist |
| `.cursor/rules/mulholland-real-estate.mdc` | Cursor project conventions |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- **MySQL 8** reachable from your machine (local install, or Heroku addon / JawsDB / etc.)
- **n8n** workflow URL when you want live triage (optional until the webhook exists)

## Database (MySQL)

### Local development

`appsettings.Development.json` includes an example `ConnectionStrings:DefaultConnection` pointing at `127.0.0.1` — adjust user/password/database name to match your MySQL instance, then:

```bash
cd API/MulhollandRealEstate.API
dotnet ef database update
```

On startup, migrations run and **30** tickets are seeded if the table is empty.

### Heroku

Heroku MySQL addons (e.g. JawsDB) usually expose a **`mysql://user:pass@host:port/dbname`** URL as **`DATABASE_URL`** (sometimes `JAWSDB_URL` / `CLEARDB_DATABASE_URL`). The API resolves that automatically via `Configuration/MysqlConnectionResolver.cs` and connects with **`SslMode=Required`** and **`TrustServerCertificate=true`** by default (override under **`MySql:`** in config if your provider needs different SSL settings).

Set **either**:

- **`DATABASE_URL`** = `mysql://...` from the addon, **or**
- **`ConnectionStrings__DefaultConnection`** = a full Pomelo/ADO.NET MySQL connection string

Do **not** commit credentials. In the Heroku dashboard: Config Vars, or CLI `heroku config:set`.

For **`dotnet ef database update`** against the remote DB, point env at the same URL (e.g. export `DATABASE_URL` before running the command) or set `ConnectionStrings__DefaultConnection` for that shell session.

## Triage: n8n webhook

The API **POST**s a JSON payload to **`N8n:TriageWebhookUrl`** (see `Services/N8nTriageDtos.cs`). Your n8n workflow should return JSON the API can parse, for example:

```json
{
  "predictedCategory": "Plumbing",
  "predictedUrgency": "Emergency",
  "confidence": 0.82,
  "tags": ["water", "safety"],
  "riskNotes": "Short note"
}
```

The parser also accepts common n8n shapes: a top-level **array** (first item), or an object with a **`json`** or **`body`** property containing those fields.

Until the webhook URL is set, or if the request fails, triage uses **heuristics** (`triageSource` = `heuristic`). Successful webhook triage sets `triageSource` = `n8n`.

### Configuration

In `appsettings.json` / `appsettings.Development.json`, or **user secrets**:

```bash
cd API/MulhollandRealEstate.API
dotnet user-secrets set "N8n:TriageWebhookUrl" "https://your-n8n.example/webhook/..."
```

Optional auth headers (same pattern as many n8n setups):

- `N8n:ClientId` / `N8n:ClientSecret` → sent as `X-Client-Id` / `X-Client-Secret` by default (names overridable).

Set **`N8n:Enabled`** to `false` to force heuristics even when a URL is configured.

Human review is flagged when confidence is below **`Triage:HumanReviewBelowConfidence`** (default `0.55`).

## Run the API

```bash
cd API/MulhollandRealEstate.API
dotnet run --launch-profile http
```

Swagger: `/swagger`, API base `http://localhost:5278`.

## Run the client

Serve `Client/` over HTTP (not `file://`), e.g.:

```bash
cd Client
python3 -m http.server 5500
```

Open `http://localhost:5500` and point **API base** at the API if needed.

## API highlights

- `GET /api/tickets` — list; query `urgency`, `needsHumanOnly`
- `GET /api/tickets/summary`
- `GET /api/tickets/{requestNumber}`
- `POST /api/tickets/{requestNumber}/triage` — calls n8n (or heuristic)
- `POST /api/tickets/triage-all`

`actualCategory` / `actualUrgency` are **sample labels** for evaluation; predictions go in `predicted*`.

## Course artifacts

See `Intro meeting for group 2.txt` and `Group Project 2 - Business Startup.pdf`.

## Docker (API only)

`API/MulhollandRealEstate.API/Dockerfile` builds the API. Pass **`DATABASE_URL`** or **`ConnectionStrings__DefaultConnection`** at runtime so the container can reach your Heroku (or other) MySQL instance.
