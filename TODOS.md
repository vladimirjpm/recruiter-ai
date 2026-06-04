# TODOS

Items identified during eng-review. Address before Stage 2 merge.

## ~~P1 — Value comparers missing~~ FIXED (2026-06-04)

`ValueComparer` добавлен в `CvGenerationBatchConfiguration` и `EvaluationConfiguration`.
5 тестов в `ValueComparerTests.cs` подтверждают корректное отслеживание мутаций.

## ~~P1 — Review findings~~ FIXED (2026-06-04)

Staff Engineer pre-Stage-2 review: 4 issues fixed.
- DTO validation (`[Required]`, `[MinLength]` on `UpsertPositionDto`) → 400 instead of 500 on bad input
- Explicit `CvGenerationBatchId` FK on `Candidate` entity → Stage 2 can set FK directly
- CORS middleware order fixed (`UseCors` before `UseHttpsRedirection`) → preflight won't break in HTTPS
- 2 validation integration tests added (missing title → 400, empty RequiredSkills → 400)

## P2 — Migration strategy notes (no action needed in Stage 1)

### Finding 2: NOT NULL columns added to non-empty tables
`AddEvaluationAuditFields` adds `PromptVersion`, `SchemaVersion`, `Temperature`, `EvaluationDurationMs`
with `nullable: false, defaultValue: ""`. Safe now (table is empty). In future stages: always
add NOT NULL column as nullable → backfill → make NOT NULL.

### Finding 7: pgvector migration path for `candidate_sections.embedding`
Column is `jsonb` in Phase 1. PostgreSQL cannot `ALTER COLUMN TYPE jsonb → vector(1536)` directly.
Phase 2 migration must:
1. `ADD COLUMN embedding_vec vector(1536)`
2. (optional) backfill from jsonb if any data exists
3. `DROP COLUMN embedding`
4. Rename or leave as `embedding_vec` and update the EF config.

## P3 — Deferred review findings (address later)

- [ ] Finding 4: Test isolation — `PositionsIntegrationTests` shares one InMemoryDb across all tests;
      `List_AfterCreatingTwo_ReturnsBoth` uses `>= 2` to compensate. Acceptable for Stage 1.
      Fix before Stage 3: use per-test db name or `IAsyncLifetime` reset.
- [ ] Finding 6: CI coverage collection — add `--collect:"XPlat Code Coverage"` to `dotnet test` step.

## P4 — Known implementation gaps (Stages 2–6 not started)

- [ ] Stage 2: CV upload (multipart, PdfPig), GET/DELETE /api/candidates
- [ ] Stage 3: PDF text extraction, `IResumeParser` → `CandidateSection` rows
- [ ] Stage 4: GPT-4o mini evaluation pipeline, `IResumeEvaluationService`
- [ ] Stage 5: React frontend scaffold
- [ ] Stage 6: CV generation, `CvGenerationBatch` workflow
