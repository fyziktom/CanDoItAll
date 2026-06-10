# process-runtime-live-openai-verification-host-alpha-v1

## Status
Completed.

## Validation Summary
- Bundle preparation status: `Prepared after structural repair`.
- Bundle readiness gate: `Passed prepared-stage validator on 2026-06-10`.
- Execution status: `Completed; build, unit, focused integration, live OpenAI, and source-scan proof captured`.
- Subbundle gate review: `Passed for SB001-SB060; critical gates cite manifests and semantic invariants`.
- Final closure gate: `Passed completed-stage validator on 2026-06-10; transcript captured under bundle://proof/SB060/transcripts/completed-validator.txt`.
- Browser validation analytics: `Passed by UI drift scan; no Razor, CSS, wwwroot, or media files changed`.

## Purpose
This bundle follows the completed process-runtime restoration work. It intentionally shifts from proving deterministic runtime basics to two higher-value areas:

1. run a guarded, source-backed live OpenAI process smoke now that API credits are available;
2. introduce the first **verification-only** generic process-driver runtime host alpha with registry, selector, DI, and manager-readonly command surfaces, while keeping execution-capable drivers blocked.

## Why this bundle exists
The previous bundle proves that processes can start, dispatch, finalize, project artifacts, and show UI readbacks through deterministic/fake-provider slices. It also proves that the app can start and that UI/project-structure process launch works. The remaining strategic gap is not another read-only driver package; it is a controlled bridge from explicit read-only domain verification into a generic host shape that can eventually evolve into runtime driver infrastructure.

## High-Level Scope
- Reconcile current branch, code, test results, and previous live-smoke skip.
- Run actual live OpenAI smoke when key is present, with explicit low budget and timeout.
- Add live direct-agent process smoke and optional live manager diagnostic smoke without logging secrets.
- Create a verification-only process driver runtime host alpha.
- Add explicit driver registry and selector limited to read-only verification lanes.
- Add DI registration for verification-only host only.
- Add manager-readonly command/API/service surface for diagnostics only.
- Add immutable verification audit persistence or a migration-ready persistence boundary.
- Add scheduler/workflow readiness guardrails without enabling driver execution hooks.
- Keep Process Core generic and dependency-clean.
- Keep execution-capable driver host blocked behind a future approval gate.

## Bundle Shape
- 20 phases.
- 60 subbundles.
- Critical gate every third subbundle.
- XLSX checklist under `evidence/checklists`.
- Large-screen browser proof only.

## Required Validation
- `dotnet build CanDoItAll.slnx --configuration Debug`
- full unit tests
- focused process runtime integration tests
- focused driver/host unit tests
- focused manager-readonly integration tests
- large desktop Playwright for process launch/run detail when UI-visible surfaces are touched
- guarded live OpenAI smoke when API key is present
- source scans for Core reverse dependency, driver mutation/runtime-host drift, secret leakage, DI/registry/selector scope, bundle-path coupling, stubs, and UI/media drift
- prepared and completed bundle validators
- red-team proof rejecting report-only, deterministic-only, and secret-leaking live proof

## Live OpenAI policy
If `OPENAI_API_KEY` is present and the user has not explicitly disabled live tests, Codex should run the live smoke with local per-command variables:

```powershell
$env:CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE='true'
$env:CANDOITALL_LIVE_OPENAI_MAX_TOKENS='800'
$env:CANDOITALL_LIVE_OPENAI_TIMEOUT_SECONDS='120'
```

The test must never print secret values. It must record only presence/absence, model/provider identifiers that are not secrets, budget/timeout, token/cost estimate if available, status, and redacted diagnostics.

