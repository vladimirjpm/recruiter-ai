# Recruiter AI

AI-powered CV screening with GPT-4o mini. Phase 1 is an embedding-free MVP; the schema is RAG-ready (pgvector is installed but not yet used).

## Stack

- .NET 10 Web API · EF Core 10 · Npgsql
- PostgreSQL 16 + pgvector (Phase 2)
- React 18 + Vite + Tailwind CSS
- OpenAI GPT-4o mini
- Railway (API + PostgreSQL) · Vercel (UI)

## Structure

```
src/
  RecruiterAi.Api/             # ASP.NET Core Web API · Program.cs · Swagger · health · logging
  RecruiterAi.Domain/          # Entities · Enums · Service interfaces
  RecruiterAi.Infrastructure/  # AppDbContext · EF configurations · migrations · DI
  RecruiterAi.Tests/           # xUnit
docker-compose.yml             # pgvector/pgvector:pg16 for local development
.github/workflows/ci.yml       # GitHub Actions — build + test
```

## Local setup

```powershell
# 1. Copy env file and set your OpenAI key
cp .env.example .env
# Edit .env — set LLM_API_KEY=sk-...

# 2. PostgreSQL + API via Docker Compose
docker compose up -d

# — OR run without Docker —

# 2a. PostgreSQL only
docker compose up -d postgres

# 2b. Apply migrations
dotnet ef database update `
  --project src/RecruiterAi.Infrastructure `
  --startup-project src/RecruiterAi.Api

# 2c. API (reads Llm__ApiKey from environment or .env)
dotnet run --project src/RecruiterAi.Api
```

- Swagger: <http://localhost:5150/swagger>
- Health:  <http://localhost:5150/health>

### Frontend

```powershell
cd frontend
npm install
npm run dev   # http://localhost:5173 — /api proxied to :5150
```

`VITE_API_URL` is not set in dev — Vite proxies `/api` to `:5150` automatically.

## Deployment

### Railway (API + PostgreSQL)

1. Create a new Railway project, add a **PostgreSQL** service (pgvector is included).
2. Connect the GitHub repo — Railway picks up `railway.toml` and builds via `Dockerfile`.
3. Set environment variables in Railway dashboard:

| Variable | Value |
|---|---|
| `Llm__ApiKey` | `sk-...` |
| `Llm__Model` | `gpt-4o-mini` |
| `ConnectionStrings__Postgres` | Railway auto-injects via `${{Postgres.DATABASE_URL}}` — or set manually |
| `Cors__AllowedOrigins__0` | `https://your-app.vercel.app` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

> **pgvector check:** before first deploy, verify `CREATE EXTENSION IF NOT EXISTS vector;` runs successfully on the Railway PostgreSQL instance. EF migrations run automatically on startup.

### Vercel (Frontend)

1. Import the GitHub repo in Vercel.
2. Set **Root Directory** to `frontend`.
3. Vercel picks up `frontend/vercel.json` — build command and SPA rewrites are pre-configured.
4. Set environment variable:

| Variable | Value |
|---|---|
| `VITE_API_URL` | `https://your-app.railway.app` |

5. Add the Vercel deployment URL to Railway's `Cors__AllowedOrigins__0`.

## CI/CD (Phase 1)

GitHub Actions (`.github/workflows/ci.yml`) — runs on every push and PR to `main`:

- **Backend**: `dotnet restore` → `dotnet build` (Release) → `dotnet test`.
- **Frontend**: `npm ci` → `npm run build` (the job is enabled automatically once `web/package.json` appears).

There is intentionally no production deployment pipeline in Phase 1 — Railway (API) and Vercel (UI) deploys are triggered manually.

## Logging

The project uses the built-in `ILogger<T>`. EventIds are centralised in [`LogEvents.cs`](src/RecruiterAi.Api/Logging/LogEvents.cs).

**What is logged:**

| Event                              | EventId   | Stage |
|------------------------------------|-----------|-------|
| Position create / update / delete  | 1001–1003 | 2 |
| CV upload start / complete / reject| 2001–2003 | 3 |
| PDF parse success / failure        | 2010–2011 | 3 |
| OpenAI request start / end / fail  | 3001–3003 | 4 |
| Evaluation completed / failed      | 3010–3011 | 4 |
| Generation batch start / end / fail| 4001–4003 | 6 |

**What is NEVER logged (PII / secrets):**

- Full CV `raw_text` — only `len=…,h=…` via `PiiSafe.Fingerprint()`.
- Candidate email — only masked (`j***@example.com`) via `PiiSafe.MaskEmail()`.
- Phone, full prompt, OpenAI responses containing candidate fields.
- OpenAI API keys, connection strings.

Production uses JSON logs (`AddJsonConsole`). Development uses readable text. Serilog / OpenTelemetry can be added in Phase 2+ if the need arises.

## Security

### Prompt injection (Stage 4)

CV text is treated as **untrusted content**:

- The system prompt explicitly instructs the model to ignore any instructions found inside the CV.
- CV text is wrapped in `<cv>...</cv>` delimiters (an explicit trusted/untrusted boundary).
- Model responses use a strict JSON Schema (`response_format`), and the score is validated server-side (0–100).

### Other measures

- **CVs are untrusted input**: no part of `raw_text` is ever interpreted as a command.
- **Logs without PII**: `raw_text`, email, phone, full prompts — never appear in logs.
- **Secrets via environment variables only**: `OpenAI__ApiKey`, `ConnectionStrings__Postgres` are read from environment variables (see `.env.example`). They are never committed.
- **File upload limits** (Stage 3): only `application/pdf`, file size cap, max 10 files per request.
- **Demo auth**: Phase 1 has no authentication. For production, JWT + tenant isolation + audit logs would be required (out of MVP scope).

## QA Plan

### Manual scenarios

Run a full smoke test after every significant change:

- [ ] Create a position (title + description + required_skills)
- [ ] Upload 1 PDF — candidate is created and `raw_text` is extracted
- [ ] Upload 5 PDFs as a single batch
- [ ] Reject non-PDF files (e.g. `.docx`, `.txt`) — 400 with a clear error message
- [ ] Evaluate all candidates for a position — every candidate gets a score
- [ ] Ranking — candidates are sorted by `score DESC`
- [ ] Open candidate details — strengths/weaknesses/matched_skills/missing_skills/interview_questions are visible
- [ ] Generate 10 synthetic CVs for an existing position
- [ ] Generated CVs are visibly tagged **"Generated" / "Synthetic"** in the UI (clearly distinguishable from uploaded CVs)
- [ ] Evaluate generated CVs through the same pipeline
- [ ] "Excellent" generated candidates usually score above the "weak" ones
- [ ] Run the full cycle (create position → generate → evaluate) for a **non-IT profession**: bus driver, electrician, cleaner. The evaluator should produce meaningful scores and interview questions for the domain.

### Automated tests

Minimum coverage for Phase 1 (in `RecruiterAi.Tests`):

- **Unit · PDF parser**: text extraction from a sample PDF / mock (Stage 3).
- **Unit · Score validation**: score → `MatchLevel` mapping, rejecting out-of-range values (Stage 4).
- **Unit · Generator request validation**: `count` ∈ [1, 30], default 10, reject out-of-range (Stage 6).
- **Integration · Positions CRUD**: `WebApplicationFactory` + Testcontainers PostgreSQL or in-memory provider (Stage 2).
- **Integration · Candidates upload** (if practical without large mock PDFs): happy path + reject non-PDF (Stage 3).

Heavy tooling (Kubernetes, ELK, Sentry, SonarQube, full deployment automation) is intentionally out of scope for Phase 1.
