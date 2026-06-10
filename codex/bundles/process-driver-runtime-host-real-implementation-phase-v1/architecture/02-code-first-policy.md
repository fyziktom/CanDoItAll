# Code-First Implementation Policy

## Why This Exists
Recent bundles generated too many coordination artifacts relative to production/test changes. This bundle must reverse that ratio.

## Required Ratio
At final closure, Codex must record:

```powershell
git diff --numstat <start-sha>...HEAD
```

Then group changed lines by:

- `src/`
- `tests/`
- `docs/`
- `codex/bundles/`
- other

Completion is blocked unless:

```text
(src + tests changed lines) >= 3 × (codex/bundles changed lines)
```

Docs are useful, but docs are not counted as implementation for this gate.

## Bundle Footprint Cap
Execution should update only:

- `reviews/01-execution-report.md`
- concise proof manifests for completed implementation phases
- final numstat/proof transcripts

Do not regenerate subbundle README files or create a large proof forest.

## File Size Guard
No new production file should exceed 350 lines unless the subbundle explicitly justifies it and creates a follow-up split plan. Existing files that grow beyond 450 lines must be split unless they are generated/migration files.
