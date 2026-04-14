## LocalAPI

This is a copy of the main API, configured for **local testing** against your **local n8n production webhook** by default.

- **Default n8n webhook**: `http://localhost:5678/webhook/5abc8a65-a151-4cf8-a5f0-1708419532fa`
- **Ports**: `http://localhost:5288` (so it doesn't collide with the main API on 5278)

### Run

```bash
cd API/LocalAPI
dotnet run --launch-profile testing-http
```

### Configure DB

Use the same env vars as the main API:

- `DATABASE_URL` = `mysql://user:pass@host:port/dbname`
- or `ConnectionStrings__DefaultConnection` = ADO.NET MySQL connection string

