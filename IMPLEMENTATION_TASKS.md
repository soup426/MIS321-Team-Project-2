# Mulholland RE — implementation task list

Use this as the team’s working backlog. Check boxes as you complete work; add rows when scope grows.

## Phase 0 — Environment

- [x] `dotnet ef database update` succeeds against MySQL (local or Heroku URL in env) — *verify on each machine*
- [x] API runs (`dotnet run`) and Swagger loads
- [x] Client loads tickets from API (dashboard at `http://localhost:5288/` when using LocalAPI; static files served by dotnet)
- [x] n8n triage webhook URL configurable; responses parse (or heuristic fallback works)

## Phase 1 — Core product (MVP)

- [x] Dashboard shows tickets with predicted urgency, confidence, tags, risk notes
- [x] Filter by urgency, “needs human”, status (Open/Closed), assignee
- [x] Single-ticket and batch triage actions
- [ ] Human-review threshold (`Triage:HumanReviewBelowConfidence`) tuned with sponsor input
- [x] README + intro notes kept current (this file + `Intro meeting for group 2.txt`)

## Phase 2 — AI quality & evaluation

- [x] Compare predictions to `actualCategory` / `actualUrgency` (summary endpoint + per-row match flags where labels exist)
- [ ] Iterate n8n workflow (LLM nodes, prompts, branching) for better accuracy
- [ ] Define team metrics (accuracy, calibration, false negatives on emergencies) — sponsor: “we handle metrics for severity”
- [ ] Log triage failures and API errors for demo debugging (structured logs / correlation id)

## Phase 3 — Property / ops features

- [ ] Property / building / unit drill-down views (beyond table columns)
- [x] Persist staff actions: notes, close/reopen, assign (events table + maintenance_requests columns)
- [ ] Role-based access (optional; likely out of scope unless required)
- [ ] Optional: GridStack stats widgets (drag/drop KPI tiles above ticket table; persisted layout)

## Phase 4 — Bonus (sponsor ideas from intro meeting)

- [ ] Image upload + storage + pass image context to triage
- [ ] Mobile-friendly client layout / PWA (responsive pass; native app = stretch)
- [x] Employee + assignment model (DB + API + dashboard assign) — *auto-assign by skillset still open*
- [ ] “Recommended assignee” by skill / load (rules engine or n8n + optional API endpoint)

## Phase 5 — Course deliverables

- [ ] Business hypothesis document
- [ ] Pitch / demo video
- [ ] Final submission package (Blackboard: repo link, docs, video)
- [ ] 360 feedback as required

## Technical debt / hygiene

- [ ] CI build (optional): `dotnet build` on push
- [x] Heroku `DATABASE_URL` / connection string documented (see README)
- [x] LocalAPI `Program.cs` serves `Client/` static files when `../Client` exists

## Suggested next additions (not in sponsor contract — useful)

- Export CSV of filtered tickets for sponsor review
- SLA columns (time open, time to first triage)
- Clear “Privacy Badger / adblock” note in README for anyone using strict blockers (allowlist `localhost` + same-origin assets)
