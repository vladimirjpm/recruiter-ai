# Recruiter AI

> AI-powered CV screening that ranks candidates against a job description in seconds, with token-level cost accounting and reproducible synthetic-CV evaluation.

[![CI](https://github.com/vladimirjpm/recruiter-ai/actions/workflows/ci.yml/badge.svg)](https://github.com/vladimirjpm/recruiter-ai/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://react.dev/)
[![Live API](https://img.shields.io/badge/API-live-4ade80)](https://recruiter-ai-production-992b.up.railway.app/health)
[![Live UI](https://img.shields.io/badge/UI-live-4ade80)](https://recruiter-ai-gamma-woad.vercel.app)

| | |
|---|---|
| 🌐 **Live UI** | <https://recruiter-ai-gamma-woad.vercel.app> |
| ⚙️ **Live API** | <https://recruiter-ai-production-992b.up.railway.app/health> |
| 📋 **Plan** | [recruiter-ai-plan.html](recruiter-ai-plan.html) — full architecture & roadmap |

---

## What it does

A recruiter pastes a job description, uploads CVs (or generates synthetic ones), and gets each candidate scored 0–100 with explained strengths, weaknesses, matched/missing skills, red flags, and ready-to-ask interview questions. The same scoring pipeline runs against synthetic CVs labelled with an expected fit level — so the evaluator can be validated end-to-end without real candidate data.

**Working on it now:**

1. Open the live UI.
2. **Paste JD tab** → paste a job description → AI pre-fills title, country, seniority, required & nice-to-have skills with **evidence quotes** on hover.
3. Upload one or more PDF CVs, or click **Generator** to synthesize a batch of CVs across 10 quality categories.
4. **Find Matching Existing Candidates** — on the Screening page, click the button to instantly discover candidates from other positions ranked by required-skill overlap. No OpenAI calls — pure text matching, sub-second. Select and attach; full AI screening runs afterward through the normal pipeline.
5. Click **Screen** — every candidate gets a structured evaluation in 3–5 seconds.
6. Sort by score, open a candidate to see reasoning, export to CSV.

### What proves it actually works

- ✅ **Non-IT roles**: tested with bus driver / electrician / cleaner JDs — evaluator produces meaningful scores and domain-specific interview questions.
- ✅ **Multi-language**: tested with a Hebrew JD (`נהג/ת אוטובוס`) — extraction and scoring work.
- ✅ **Synthetic CV validation**: generator produces CVs across 10 expected-fit levels; "excellent" candidates score above "weak" ones with high consistency.

---

## Stack

**Backend** — .NET 10 Web API · EF Core 10 · PostgreSQL 16 + pgvector (Phase 2-ready) · OpenAI SDK · PdfPig · xUnit
**Frontend** — React 18 · TypeScript · Vite · Tailwind CSS · React Query · React Router · axios · react-hot-toast
**Infra** — Docker (multi-stage) · GitHub Actions (CI) · Railway (API + Postgres) · Vercel (UI)

---

## Engineering highlights

These are the things worth pointing at in a review:

- **Clean Architecture** (Domain / Infrastructure / Api) — service interfaces in Domain, OpenAI/Npgsql swaps live in Infrastructure, no leakage upward.
- **Prompt injection defence** (Stage 4) — CV text wrapped in `<cv>…</cv>` delimiters, system prompt explicitly instructs the model to ignore CV-embedded instructions, response shape enforced by strict JSON Schema (`response_format`), score validated server-side.
- **PII-safe logging** — raw CV text, emails, phones, full prompts never appear in logs. Only `len=…,h=…` fingerprints via `PiiSafe.Fingerprint()`. JSON logs in Prod, readable text in Dev.
- **Rate limiting on cost endpoints** — `/screen` and `/generate` are partitioned per-IP, fixed window 10/min, protects the OpenAI budget from runaway scripts.
- **Phase 2-ready schema** — `candidate_sections` + `vector(1536)` columns are migrated and waiting; pgvector extension is enabled. Swapping `ICandidateSearchService` to a pgvector implementation needs no schema migration.
- **CORS hardened in Production** — empty `Cors:AllowedOrigins` or `"*"` fails fast at startup. No silent open-relay.
- **EF migrations on startup, gated** — auto-migrate runs only for relational providers (skipped for `InMemoryDatabase` in integration tests).
- **Generated CVs flow through the exact same evaluation pipeline as uploaded CVs** — no separate scoring path, no test-only branch. Validates the scoring logic across candidate quality levels and job domains.
- **Synthetic CV cost accounting** — every evaluation persists input/output tokens and estimated USD cost. Visible in the UI footer.
- **OpenAI observability** — every LLM call logs model name, latency (ms), input/output tokens, and estimated USD cost as a structured JSON line. Single `grep` in Railway logs gives a full cost breakdown by operation.
- **Health split** — `/health/live` (process only) and `/health/ready` (process + DB) via ASP.NET Core tag-based routing. Liveness probe never touches the database, so a transient Postgres blip can't trigger a container restart.

---

## Architecture

```
┌──────────────────┐    HTTPS    ┌──────────────────┐    HTTPS    ┌──────────────┐
│ React + Vite UI  │ ───────────▶│  .NET 10 Web API │ ───────────▶│   OpenAI     │
│  (Vercel)        │             │   (Railway)      │             │  GPT-4o mini │
└──────────────────┘             └────────┬─────────┘             └──────────────┘
                                          │
                                          ▼
                                 ┌──────────────────┐
                                 │  PostgreSQL 16   │
                                 │  + pgvector      │
                                 │  (Railway)       │
                                 └──────────────────┘

src/
  RecruiterAi.Domain/          # Entities, Enums, service interfaces (no infrastructure refs)
  RecruiterAi.Infrastructure/  # AppDbContext, EF configurations, migrations, OpenAI/PdfPig adapters, DI
  RecruiterAi.Api/             # ASP.NET Core: controllers, Program.cs, Swagger, health, structured logging
  RecruiterAi.Tests/           # xUnit — 100 tests (unit + integration via WebApplicationFactory)
frontend/
  src/pages/      ScreeningPage · GeneratorPage
  src/components/ CreatePositionModal (with Paste JD tab) · DetailDrawer · DropZone · etc.
docker-compose.yml             # pgvector/pgvector:pg16 for local dev
Dockerfile                     # Multi-stage SDK→runtime, port 8080
railway.toml · frontend/vercel.json
.github/workflows/ci.yml       # build + test on every push to main
```

---

## Local setup

**Prerequisites:** Docker Desktop · Node.js 18+

**Step 1 — create `.env` and fill in your OpenAI key**

```cmd
copy .env.example .env
notepad .env
```

In the editor, set `LLM_API_KEY=sk-...` (copy from <https://platform.openai.com/api-keys>).  
`Llm__ApiKey` further down in the file is for `dotnet run` — also fill it with the same key if you plan to run without Docker.

> Git Bash / macOS: use `cp .env.example .env`

**Step 2 — start Postgres + API**

```cmd
docker compose up -d
```

Expected output: `Container recruiter-ai-postgres  Healthy` · `Container recruiter-ai-api  Started`

**Step 3 — start the frontend**

```cmd
cd frontend
npm install
npm run dev
```

Open the URL printed by Vite (usually <http://localhost:5173>, may be 5174 if 5173 is taken).  
API Swagger: <http://localhost:5150/swagger>

<details>
<summary>Run API without Docker (requires .NET 10 SDK)</summary>

```cmd
docker compose up -d postgres
dotnet ef database update --project src/RecruiterAi.Infrastructure --startup-project src/RecruiterAi.Api
dotnet run --project src/RecruiterAi.Api
```

</details>

Endpoints:
- Swagger UI — <http://localhost:5150/swagger>
- Health (liveness) — <http://localhost:5150/health/live>
- Health (readiness + DB) — <http://localhost:5150/health/ready>

---

## Deployment

**Current flow:** GitHub Actions runs build + tests + frontend lint/typecheck/build on every push to `main`. Deploy is triggered manually after green CI — Railway rebuilds from the latest `main`, Vercel picks up the push automatically.

**Planned:** CI success → automatic deploy. Railway exposes a Deploy Hook (webhook URL); the Actions workflow would call it via `curl` only after all checks pass. Vercel supports the same pattern via `vercel deploy --prod` in CI. Not implemented yet — single-contributor project, manual gate is sufficient for now.

The project deploys to **Railway** (API + Postgres) and **Vercel** (UI). Both pick up config from files committed in the repo:

- `Dockerfile` — Railway builds via dockerfile builder
- `railway.toml` — healthcheck path, restart policy
- `frontend/vercel.json` — Vite preset, SPA rewrites

### Railway environment variables

| Variable | Value |
|---|---|
| `Llm__ApiKey` | `sk-...` |
| `Llm__Model` | `gpt-4o-mini` |
| `ConnectionStrings__Postgres` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true` |
| `Cors__AllowedOrigins__0` | `https://your-app.vercel.app` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

> Mount a Railway Volume at `/app/uploads` for CV file persistence across deploys.

### Vercel environment variables

| Variable | Value |
|---|---|
| `VITE_API_URL` | `https://your-app.railway.app` |

Set **Root Directory** to `frontend`.

---

## Testing

- **100 tests** in `RecruiterAi.Tests` — unit + integration via `WebApplicationFactory<Program>` with `InMemoryDatabase`.
- **Integration coverage** — Positions CRUD, Candidates upload (extension/magic-byte/Content-Type checks), Evaluations (`/screen`, `/evaluations`, CSV export), Generator, AI extraction validation (`/extract` with input-length guards + 503 on extractor failure).
- **Smoke tests** (`OpenAiSmokeTests`, `GeneratorSmokeTests`) — opt-in, hit real OpenAI when `LLM__APIKEY` is set.

```powershell
dotnet test src/RecruiterAi.Tests
```

CI runs the same on every push to `main`.

---

## Security

| Concern | Mitigation |
|---|---|
| Prompt injection from CV content | System prompt instructs model to ignore CV instructions; CV wrapped in `<cv>` delimiters; response shape enforced by JSON Schema; score validated server-side (0–100) |
| Untrusted file uploads | `application/pdf` only · `%PDF` magic bytes verified · 5 MB per file · 25 MB per request · max 10 files |
| Secrets in logs | `PiiSafe.Fingerprint(file)` and `PiiSafe.MaskEmail(addr)` — raw text, emails, phones, full prompts never logged |
| Path traversal on `/file` endpoint | Absolute paths rejected; resolved path must start with the configured upload root (`StringComparison.Ordinal`) |
| Open CORS | `Cors:AllowedOrigins` required in Production; `"*"` rejected at startup |
| OpenAI budget abuse | Rate limiter on cost endpoints (`/screen`, `/generate`): 10/min per IP, fixed window |
| `rawText` exposure in list APIs | Generated CV text is only available via `GET /api/candidates/{id}/resume-text` (gated to `Source=Generated`); never serialised in `GET /api/candidates` |

**Out of scope for Phase 1 (demo mode):** JWT auth, tenant isolation, audit logs.

---

## Roadmap

**Phase 1 — done** ✅
Backend foundation · CV upload + parse · OpenAI screening · synthetic CV generator · React UI · Paste-JD AI extraction · deployment.

**Phase 2 — semantic search (when scale demands it)**
Activate `candidate_sections` table · generate embeddings with `text-embedding-3-small` · swap `ICandidateSearchService` to a pgvector implementation · `IVFFlat` index on `embedding`. No schema migration required.

**Production hardening (deferred from Phase 1)**
JWT auth + tenant isolation + audit logs · multi-instance migration story (controlled CI step, not on-startup) · Sentry / OpenTelemetry · per-tenant rate limit partitioning.

**Also shipped (post-Phase 1)**
Recruiter score override — manual adjustment ± delta, clamped to 0–100, with audit trail (original score, delta, comment, timestamp).

---

## License

MIT.
