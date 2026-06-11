# SB08: Release decision and red-team closure

## Objective
Produce an honest merge/readiness decision.

## Required proof
- Build 0 warnings/errors.
- Full unit tests.
- Focused integration matrix.
- Large desktop Playwright proof.
- Optional live OpenAI classification.
- Source scans:
  - Process Core dependency drift,
  - driver execution-capable hooks,
  - reflection discovery/fallback selector/self-registration,
  - mutation APIs in verification/dry-run paths,
  - secret leakage,
  - bundle-path coupling,
  - large-file growth.
- Code-first ratio using explicit start SHA.

## Release decision format
Answer one of:
- `Merge-ready for maf-processes-refactor -> development`
- `Runtime-ready but UI/live blocked`
- `Not merge-ready`

Include the exact blockers if not merge-ready.

## Do not do
- Do not close if SB08 ratio fails.
- Do not claim live proof from skipped tests.
- Do not treat process-mock proof as live provider proof.
