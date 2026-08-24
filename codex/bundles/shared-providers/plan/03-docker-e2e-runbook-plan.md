# Docker E2E runbook plan

## Planned tracked files

- `compose.shared-providers.e2e.yaml`
- `.env.shared-providers.e2e.example`
- `tools/SharedProviders/Start-SharedProviderE2E.ps1`
- `tools/SharedProviders/Start-SharedProviderE2E.sh` or one cross-platform Python entrypoint
- `tools/SharedProviders/Stop-SharedProviderE2E.*`
- `tools/SharedProviders/Run-SharedProviderE2E.*`
- non-production E2E orchestrator project
- deterministic upstream test-support project
- `docs/runbooks/shared-providers-e2e.md`

All script comments and code comments are English.

## Start flow

1. verify Docker/Compose and ports;
2. create ignored artifact directories;
3. generate ephemeral credentials without echo;
4. initialize independent PostgreSQL databases;
5. build/tag app image once;
6. build deterministic upstream once;
7. start dependencies and wait for health;
8. configure central through application services;
9. configure client sources/imports through application services;
10. start/refresh app services;
11. run scenario suite;
12. write JSON and Markdown reports.

## Reset flow

The orchestrator may delete only the dedicated ignored E2E root and dedicated databases after
an explicit `--reset` flag. It must refuse an ambiguous or production-looking connection.

## Final flow

SB12 uses a clean reset, runs all scenarios, checks health/log redaction, writes handoff, and
does not stop services.

## Manual workflow

The runbook must tell the operator how to:

- open central/client UIs;
- find local/shared providers;
- run a simple chat/agent;
- test image generation;
- unpublish/re-publish;
- stop/restart central;
- sync both clients;
- inspect usage/audit metadata;
- find ephemeral tokens locally without printing them;
- clean up later.

## Proof artifacts

Store under ignored `.artifacts/shared-providers-e2e/handoff/`:

- `scenario-results.json`
- `container-status.txt`
- `health.json`
- `manual-handoff.md`
- sanitized log excerpts
- request capture assertions
- source/import/profile IDs excluding secrets
- source commit/image tags.
