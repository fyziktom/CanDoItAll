# Runtime Constraints And Operational Notes

## What was available in the uploaded repo

Available:
- full source tree,
- workstation project,
- tests,
- migrations,
- sample fixtures,
- docs,
- an older Codex handoff bundle.

Not available in the upload:
- the real workstation DB file with 200k+ indexed songs.

## Important environment limitation during this audit

The audit environment did not provide a working `dotnet` CLI runtime, so this package is based on static code review rather than executed build/test results.

That is why this bundle contains:
- explicit Codex implementation prompts,
- validator-agent steps,
- read-only real-DB evaluation rules.

## DB handling rule for Codex

The real indexed DB is valuable test input but must not be modified directly.

Required safe practice:
- detect the active DB provider first,
- if SQLite:
  - create a full copy of the DB file to a temp path before running any mutating workflow,
- if PostgreSQL:
  - use a temp database, temp schema, or restored snapshot,
- never point implementation experiments at the original DB.

## Why copy-first matters

Grouping will require:
- schema migration,
- new enrichment data,
- proposed groups,
- possibly embeddings.

Even a “test” run is mutating work.
Therefore “read-only but using the same DB” is not enough once migrations or enrichment writes are involved.

## Ollama operational notes for Codex

Codex may assume:
- Ollama is already available in the target environment,
- models can be pulled if missing,
- pull time may be noticeable.

Practical implementation rule:
- before running embedding generation, check model availability,
- if absent, pull once,
- wait for the pull to finish,
- then proceed.

## Existing workstation patterns worth reusing

- durable `ProcessingTask` orchestration
- incremental cursor/checkpoint logic
- one-to-one enrichment tables
- page-level maintenance actions
- task status and lane telemetry
- test fixture pattern with temp SQLite

## Strong recommendation about scope

Do not make phase 1 depend on:
- a Python sidecar,
- FAISS as a hard runtime requirement,
- an external vector database,
- a full NLP parser framework.

Those can be future upgrades.
The current codebase is strongest when the first version stays:
- C#,
- EF Core,
- Ollama HTTP integration,
- DB-provider compatible,
- operationally explicit.

## Preferred rollout stance

- implement schema and profile generation first,
- validate normalization on fixtures,
- benchmark embeddings on a copied real DB subset,
- only then run a full copied-DB dry run,
- only after validator sign-off should anyone consider broader rollout.
