# Mulholland RE — implementation task list

Use this as the team’s working backlog. Check boxes as you complete work; add rows when scope grows.

## Phase 0 — Environment

- [ ] `dotnet ef database update` succeeds against MySQL (local or Heroku URL in env)
- [ ] API runs (`dotnet run`) and Swagger loads
- [ ] Client served over HTTP (not `file://`) and loads tickets from API
- [ ] n8n triage webhook URL in user secrets (`N8n:TriageWebhookUrl`); confirm responses parse (or heuristic fallback works)

## Phase 1 — Core product (MVP)

- [ ] Dashboard shows all tickets with predicted severity/urgency and confidence
- [ ] Filter by urgency and “needs human” works end-to-end
- [ ] Single-ticket and batch triage actions tested
- [ ] Human-review threshold (`Triage:HumanReviewBelowConfidence`) tuned with sponsor input
- [ ] Document assumptions in README or sponsor-facing one-pager

## Phase 2 — AI quality & evaluation

- [ ] Compare model predictions to `actualCategory` / `actualUrgency` (summary endpoint + manual spot checks)
- [ ] Iterate n8n workflow (LLM nodes, prompts, branching) for better accuracy
- [ ] Define team metrics (accuracy, calibration, false negatives on emergencies)
- [ ] Log triage failures and API errors for demo debugging

## Phase 3 — Property / ops features

- [ ] Property / building / unit drill-down views (if not covered by filters)
- [ ] Persist staff actions (acknowledge human review, assign vendor) — **needs new tables**
- [ ] Role-based access (optional; likely out of scope unless required)

## Phase 4 — Bonus (sponsor ideas)

- [ ] Image upload + storage + pass image context to triage
- [ ] Mobile-friendly client layout / PWA
- [ ] Auto-suggest assignee by trade/skill (needs employee/skill data model)

## Phase 5 — Course deliverables

- [ ] Business hypothesis document
- [ ] Pitch / demo video
- [ ] Final submission package (Blackboard: repo link, docs, video)
- [ ] 360 feedback as required

## Technical debt / hygiene

- [ ] CI build (optional): `dotnet build` on push
- [ ] Production config: secrets, HTTPS; Heroku `DATABASE_URL` / connection string documented for the team
