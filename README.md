# MIS 321 — Team Project 2 (Mulholland Real Estate)

Property-management maintenance **triage** demo: ASP.NET Core 9 Web API (EF Core + **MySQL** via Pomelo), Bootstrap + vanilla JS client, and **n8n webhook** triage with **heuristic fallback**. Deploy or run locally with MySQL (`DATABASE_URL` or connection string). Sample data: `Sample Data/Maintenance Report Example.xlsx` → `API/LocalAPI/Data/maintenance-seed.json`.

**Sponsor notes:** see `Intro meeting for group 2.txt` (dashboard, AI triage, human review when confidence is low, tags, filters, risk — plus bonus ideas: images, mobile, skill-based assignment).

## Repo layout

| Path | Purpose |
|------|---------|
| `API/LocalAPI/` | **Main Web API:** EF migrations, seed JSON, Docker, static `Client/` on **5288** (see `README_TESTING.md`) |
| `Client/` | Static dashboard (`index.html`, `app.js`, `styles.css`); Bootstrap vendored under `Client/vendor/bootstrap/` (no CDN required) |
| `SQL/` | Optional hand-run MySQL scripts (`mulholland_schema.sql`, `mulholland_workforce.sql`) aligned with n8n/Heroku |
| `Sample Data/` | Source Excel for seeded tickets |
| `IMPLEMENTATION_TASKS.md` | Living checklist |
| `.cursor/rules/mulholland-real-estate.mdc` | Cursor project conventions |

## Secrets (do not commit)

- **Never** commit real MySQL passwords, `DATABASE_URL`, or n8n secrets in repo.
- Use **user secrets**, env vars (`DATABASE_URL`, `ConnectionStrings__DefaultConnection`), or Heroku config vars.
- `appsettings.Development.json` in this repo uses **placeholders** only. Copy `API/LocalAPI/appsettings.Development.json.example` if you want a local template.
- If credentials were ever exposed in git history, **rotate** them in the Heroku / DB provider dashboard.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- **MySQL 8** reachable from your machine (local, or Heroku addon / JawsDB / ClearDB)
- **n8n** workflow URL when you want live triage (optional; heuristic fallback if missing or failing)

## Database (MySQL)

### Connection

Heroku-style URLs: set **`DATABASE_URL`** = `mysql://user:pass@host:port/dbname`, or use **`ConnectionStrings:DefaultConnection`**. Resolved in `Configuration/MysqlConnectionResolver.cs` with **`SslMode=Required`** for typical cloud MySQL (do not use unsupported MySqlConnector options in connection strings).

### Schema

- Primary table: **`maintenance_requests`** (snake_case; same as n8n writes).
- Optional: run `SQL/mulholland_schema.sql` then `SQL/mulholland_workforce.sql` on Heroku if you use employees/events/assignment columns.

### Migrations / seed

`Database:AutoMigrate`, `Database:SeedEnabled`, and `Database:SeedDemoUsers` in appsettings control startup migrate/seed (defaults vary; see `API/LocalAPI/appsettings.json`). Run EF from **`API/LocalAPI`**: `dotnet ef database update`.

## Triage: n8n webhook

The API **POST**s JSON to **`N8n:TriageWebhookUrl`** (see `Services/N8nTriageDtos.cs`). n8n should return parseable JSON, e.g.:

```json
{
  "predictedCategory": "Plumbing",
  "predictedUrgency": "Emergency",
  "confidence": 0.82,
  "tags": ["water", "safety"],
  "riskNotes": "Short note"
}
```

The parser also accepts common n8n shapes: top-level **array** (first item), or **`json`** / **`body`** wrappers.

Configure via user secrets or env:

```bash
cd API/LocalAPI
dotnet user-secrets set "N8n:TriageWebhookUrl" "https://your-n8n.example/webhook/..."
```

Human review: **`Triage:HumanReviewBelowConfidence`** (default `0.55`). Set **`N8n:Enabled`** to `false` to force heuristics.

## Image upload

The API supports uploading real images for a ticket (stored on disk in dev/demo) and listing them later.

### Endpoints

- `POST /api/tickets/{requestNumber}/images` — multipart form upload (field name: `file`)
- `GET /api/tickets/{requestNumber}/images` — list the most recent 50 images with public URLs
- Uploaded files are served from `GET /uploads/...`

### Config

- **`Uploads:Path`**: where files are stored on disk (default: `<ContentRoot>/uploads`)
- **`Uploads:PublicBaseUrl`**: base URL used when generating absolute image URLs for n8n (example: `http://localhost:5288`)

Notes:
- For n8n to fetch images, `Uploads:PublicBaseUrl` must be reachable from the n8n runtime (e.g. use `host.docker.internal` if n8n runs in Docker).
- Allowed content types: `image/jpeg`, `image/png`, `image/webp`. Max size: 10MB per upload.

### n8n changes (if you want vision)

The n8n webhook request now includes:
- `hasImage`, `imageType`, `imageSeverityHint`, `imageUrlOrCount` (legacy)
- **`imageUrls`**: up to 3 absolute URLs (requires `Uploads:PublicBaseUrl`)

If you want the AI to actually “look” at the image, your n8n workflow needs to:
- Fetch the image bytes from `imageUrls[0]` (HTTP Request node)
- Send it to a vision-capable model node (or run OCR/labeling first)
- Merge the extracted signals into your prompt before producing `predictedCategory`, `predictedUrgency`, `confidence`, `tags`, `riskNotes`

## Run the API + dashboard (recommended)

```bash
cd API/LocalAPI
dotnet run --launch-profile testing-http
```

- **Dashboard:** `http://localhost:5288/` (static `Client/` + default `index.html`)
- **Swagger:** `http://localhost:5288/swagger`
- **Local testing notes:** `API/LocalAPI/README_TESTING.md` (demo logins, seed flags, n8n)

The client uses **`window.location.origin`** for API calls, so open the app from the same host/port as Kestrel to avoid CORS and mixed-origin issues.

## Optional UI upgrade: GridStack “stats” dashboard

It’s feasible to add [GridStack](https://gridstackjs.com/) to this repo because the client is plain static assets (no bundler required). This enables a draggable/resizable “stats widgets” area above the tickets table, with different KPIs shown as tiles.

### What you’ll get

- **Drag/drop + resize** stat widgets (persisted per-browser via `localStorage`)
- **Different stats per widget**, all computed from existing API calls
- **Zero framework**: works with the existing `Client/index.html` + `Client/app.js`

### Minimal integration plan (no API changes required)

- **Add GridStack assets** (recommended: vendor locally, like Bootstrap)
  - Put files under `Client/vendor/gridstack/`:
    - `gridstack-all.js` (or `gridstack-h5.js` depending on your preference)
    - `gridstack.min.css`
  - Add them to `Client/index.html` *after* Bootstrap CSS and *before* `app.js`.
- **Replace the fixed summary strip** (`#summaryRow`) with a GridStack container:
  - New container: `<div class="grid-stack" id="statsGrid"></div>`
  - Each widget is a `.grid-stack-item` containing a small Bootstrap card.
- **Define widgets as data**, not hardcoded DOM:
  - Create an array like `const STAT_WIDGETS = [{ id, title, compute(tickets, summary), format }]`
  - Render widgets into GridStack based on that list.
- **Compute stats from what we already fetch**
  - Use `GET /api/tickets` result (already loaded by `loadTickets()`) to compute:
    - Open vs closed
    - Unassigned count
    - Emergency / Urgent / Routine counts
    - “Needs human review” count
    - Avg confidence of triaged tickets
  - Use `GET /api/tickets/summary` result (already loaded by `loadSummary()`) for:
    - Total / triaged / needsHumanReview
    - Category + urgency match rates (for seeded sample rows)
- **Persist layout**
  - On GridStack change events, save layout (`x,y,w,h`) keyed by widget id in `localStorage`.
  - On load, restore layout if present; otherwise use default positions.

### Suggested default widgets (“different stats”)

All of these can be computed from existing endpoints:

- **Total tickets**: from summary `total`
- **Triaged**: from summary `triaged`
- **Needs human review**: from summary `needsHumanReview`
- **Open**: count `status === "Open"` from `/api/tickets`
- **Emergency (open)**: `status === "Open" && predictedUrgency === "Emergency"`
- **Unassigned (open)**: `status === "Open" && assignedEmployeeId == null`
- **Avg confidence (triaged)**: mean of `confidenceScore` where present/valid
- **Urgency match (sample)**: `urgencyMatches / comparedRows` (summary)
- **Category match (sample)**: `categoryMatches / comparedRows` (summary)

### If you want server-side stats (optional)

If the ticket list gets large, add a new endpoint like `GET /api/tickets/stats` that returns a pre-aggregated payload for the widgets. That keeps the UI snappy and avoids recomputing in JS.

## API highlights

**Tickets**

- `GET /api/tickets` — list; query: `urgency`, `needsHumanOnly`, `status` (Open/Closed), `assignedEmployeeId` (`0` = unassigned)
- `GET /api/tickets/summary`
- `GET /api/tickets/{requestNumber}`
- `POST /api/tickets` — submit test ticket (auto-triage if configured)
- `POST /api/tickets/{requestNumber}/triage` — re-run triage
- `POST /api/tickets/triage-all` — **Manager/Dispatcher only** (can be slow if n8n runs per ticket)
- `GET /api/tickets/{requestNumber}/events` — audit trail / notes
- `POST /api/tickets/{requestNumber}/notes`
- `POST /api/tickets/{requestNumber}/assign` — body `employeeId` (use `0` to unassign)
- `POST /api/tickets/{requestNumber}/close` / `POST .../reopen`

**Employees** (Manager/Dispatcher only for list/create in LocalAPI)

- `GET /api/employees`
- `POST /api/employees` — create assignee (dev/demo)

**Residents**

- `POST /api/resident/tickets` — multipart submit (anonymous); see `resident.html`

`actualCategory` / `actualUrgency` are **evaluation labels** from sample data, not resident input.

## Auth (JWT) + employee dashboards

When `Jwt:Key` is set, the API enables JWT auth and protects most endpoints.

### Config

- `Jwt:Key` (required to enable auth)
- `Jwt:Issuer` (optional)
- `Jwt:Audience` (optional)
- `Jwt:ExpMinutes` (optional, default 240)
- `Auth:AllowOpenRegistration` (default false) — if true, allows `POST /api/auth/register`

### Endpoints

- `POST /api/auth/login` — returns `{ accessToken, employee }`
- `POST /api/auth/register` — dev-only when enabled
- `GET /api/me` — current employee

### Authorization rules (LocalAPI)

- **Manager/Dispatcher**: all tickets; `GET/POST /api/employees`; batch **`POST /api/tickets/triage-all`**
- **Maintenance** (and other non-manager roles): `GET /api/tickets` defaults to **assigned-to-me** unless you pass **`assignedEmployeeId=0`** (unassigned pool). Cannot list another user’s assignee filter. Ticket detail and mutations require the ticket to be **unassigned** or **assigned to you** (otherwise **403**).
- **`POST /api/tickets`** (JSON test submit) and **`POST /api/resident/tickets`** (multipart) are **anonymous** when JWT is enabled (`[AllowAnonymous]`).
- Employee **`passwordHash`** is never returned in JSON (`[JsonIgnore]`).

## Course artifacts

See `Intro meeting for group 2.txt` and `Group Project 2 - Business Startup.pdf`.

## Docker

`API/LocalAPI/Dockerfile` builds the API. Pass **`DATABASE_URL`** or **`ConnectionStrings__DefaultConnection`** at runtime.
