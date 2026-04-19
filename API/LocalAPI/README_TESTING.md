# LocalAPI — local run and testing

The **canonical** API for this repo is **`API/LocalAPI/`** (ASP.NET Core 9, EF Core, MySQL). It serves the **`Client/`** static UI on the same port so `fetch` calls are same-origin.

## Ports and URLs

- **App + static client:** `http://localhost:5288/` (default files → `index.html`)
- **Swagger:** `http://localhost:5288/swagger`

```bash
cd API/LocalAPI
dotnet run --launch-profile testing-http
```

## Database

Set **`DATABASE_URL`** (`mysql://user:pass@host:port/db`) or **`ConnectionStrings__DefaultConnection`** (ADO.NET MySQL). Resolver: `Configuration/MysqlConnectionResolver.cs`.

Optional **`API/LocalAPI/appsettings.Secrets.json`** (gitignored template: `appsettings.Secrets.json.example`) for local credentials without committing them.

## Migrations and seed

From `API/LocalAPI`:

```bash
dotnet ef database update
```

In configuration:

- **`Database:AutoMigrate`** — apply EF migrations on startup (useful locally).
- **`Database:SeedEnabled`** — seed maintenance tickets from `Data/maintenance-seed.json`.
- **`Database:SeedDemoUsers`** — seed demo employees + skills (see below).

## JWT auth (local `appsettings.json`)

`Jwt:Key` is set in the committed local `appsettings.json` so the dashboard enforces login. Adjust or override via user secrets / env for your environment.

## Demo users (when `SeedDemoUsers` has run)

Password for all seeded demo accounts: **`demo1234!`**

| Username | Role        | Notes              |
|----------|-------------|--------------------|
| `morgan` | Manager     | Full ticket scope, employees API, batch triage |
| `alex`, `taylor`, `sam`, `pat` | Maintenance | Scoped to own + unassigned tickets per API rules |

Login: `POST /api/auth/login` with `{ "username", "password" }` → `accessToken` + `employee` object (stored by the client in `localStorage`).

## n8n

Webhook URL: **`N8n:TriageWebhookUrl`** in appsettings / secrets. If the webhook is missing, disabled, or errors, triage falls back to **heuristics** (`Services/TriageService.cs`).

**`POST /api/tickets/triage-all`** re-triages every row and can be **slow** when n8n is enabled (one HTTP call per ticket). Prefer single-ticket triage for demos unless you use a fast mock workflow.

## Resident portal

- **UI:** `http://localhost:5288/resident.html`
- **API:** `POST /api/resident/tickets` — `multipart/form-data` (anonymous). See `Controllers/ResidentController.cs`.

## Client vs API origin

Open the app from **Kestrel** (`http://localhost:5288/...`) so `window.location.origin` matches the API. Opening HTML from `file://` or another port will break API calls unless you reconfigure a base URL.
