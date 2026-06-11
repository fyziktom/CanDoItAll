# Stabilization Ledger

## Stable Surfaces
- Process template automation remains covered by focused deterministic integration proof: bundle://proof/SB05/transcripts/focused-integration-matrix.txt.
- Project/project-structure launch and completed-run readback remain covered by large desktop Playwright proof: bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt.
- Live OpenAI process-run smoke uses the managed OpenAI default provider through MAF and passed with `ModelSource=ProviderDefault`: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt.
- Boundary scans show no Process Runtime Core extraction, no dispatcher/outbox/finalizer move, no direct scheduler/workflow driver hook, and no direct provider bypass: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt.

## Freeze
- Do not start Process Runtime Core extraction on this stabilization branch.
- Do not move dispatcher, outbox, finalizer, runtime, scheduler, or workflow process services into a new process-core package.
- Do not add execution-capable drivers, fallback selectors, reflection discovery, self-registration, or hidden driver hooks.

## Next Phase
- After branch acceptance, the next phase is a seam inventory for possible runtime-core extraction.
- The inventory should document ownership, dependencies, test coverage, and candidate seams before any implementation move.
