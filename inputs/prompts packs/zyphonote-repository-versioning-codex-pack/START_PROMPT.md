You are implementing a new **repository versioning / branching / forking / merge request** layer into the current Zyphonote PHP 8.2 + MariaDB repository.

## Inputs

You have:
- the current repo (`zyphonote-web-ui-refresh`)
- this implementation pack

## Objective

Implement a **generic git-like repository domain** that supports:

- commits with author + message,
- branches,
- merge commits,
- forks,
- merge requests,
- origin status checks for offline clients,
- content-addressed blobs and snapshots,
- reusable repository graphs in the PHP app,
- API endpoints that the future Blazor WASM client can consume offline-first.

The repository layer must cover these entity roots:

- score
- learning_package
- playlist
- event

## Hard requirements

- PHP 8.2 + MariaDB
- no staging area
- every commit has:
  - author account,
  - message,
  - timestamp
- server is the source of truth
- WASM client can work offline and sync later
- repository graph must be renderable in a GitKraken-like canvas
- all code comments must be in English
- existing score / marketplace / learning / planning flows must keep working during migration
- exact hashes must exist for stored content and commit snapshots
- support forks and merge requests
- think ahead for structured score merge, even if the rich merge UI is built later

## Mandatory reading order

Read in this order before coding:

1. `README.md`
2. `CURRENT_REPO_ALIGNMENT.md`
3. `SPEC/01-executive-summary.md`
4. `SPEC/02-current-state-findings.md`
5. `SPEC/03-target-architecture.md`
6. `SPEC/04-unified-vcs-domain-model.md`
7. `SPEC/05-storage-hashing-and-canonicalization.md`
8. `SPEC/06-score-merge-strategy.md`
9. `SPEC/07-playlist-event-package-versioning.md`
10. `SPEC/08-forks-and-merge-requests.md`
11. `SPEC/09-api-contract.md`
12. `SPEC/10-php-ui-and-canvas-integration.md`
13. `SPEC/11-wasm-offline-clone-sync.md`
14. `SPEC/12-migration-plan.md`
15. `SPEC/13-testing-seed-data-rollout.md`
16. `SPEC/14-risk-register.md`
17. `API/repository-versioning.openapi.yaml`
18. `DB/2026-03-08-repository-versioning-proposed.sql`
19. all files under `CHECKLISTS/`
20. the example payloads under `EXAMPLES/`
21. the code skeletons under `CODE-SAMPLES/`

## Execution order

Execute the prompt sequence under `PROMPTS/` strictly in order:

1. `PROMPTS/01-analysis-and-incremental-plan.md`
2. `PROMPTS/02-db-and-shared-domain.md`
3. `PROMPTS/03-score-commit-and-merge-core.md`
4. `PROMPTS/04-playlist-event-package-repositories.md`
5. `PROMPTS/05-api-routes-and-authz.md`
6. `PROMPTS/06-php-ui-and-canvas-graph.md`
7. `PROMPTS/07-forks-and-merge-requests.md`
8. `PROMPTS/08-wasm-sync-contracts.md`
9. `PROMPTS/09-seeds-tests-docs.md`
10. `PROMPTS/10-final-audit.md`

## Critical modeling rules

- Do not treat the current entity tables as the only source of truth.
- Introduce a shared repository core for all four root entity types.
- Keep existing tables as read models / compatibility bridges during migration.
- Use content-addressed blobs and snapshots.
- Do not keep using version-id-based storage as the long-term model.
- For non-default branches, do not overwrite the public/main entity read model.
- Published sales, purchased content, and playlist shares must pin exact commit hashes.
- Protect default branches from destructive updates.
- Add audit coverage for branch, merge, fork, and MR mutations.
- Preserve future room for structured score merge based on stable musical ids.

Start now with `PROMPTS/01-analysis-and-incremental-plan.md`.
