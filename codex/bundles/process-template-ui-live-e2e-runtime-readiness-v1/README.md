# process-template-ui-live-e2e-runtime-readiness-v1

## Status
Prepared for Codex implementation.

## Purpose
Continue from the successful `process-template-automation-e2e-multiteam-host-readiness-v1` bundle and move from backend/process-mock automation proof to user-facing process launch confidence.

The previous bundle finally added meaningful automation-path tests for representative templates. This bundle must preserve that code-first style, but close the remaining product gaps:

- large-screen UI proof for selecting and launching representative processes from project/project-structure context,
- live OpenAI process-run proof for at least one template path when explicitly opted in,
- PostgreSQL-backed automation proof for business-analysis and software/multi-team scenarios,
- runtime-host manager readback attached to real process runs,
- scheduler/workflow-origin process launch and read-only verification job execution,
- repair/rework branch proof for at least one representative software process,
- hard boundaries preventing Process Core domain leakage and execution-capable driver side effects.

## Code-first execution policy
This bundle intentionally has only 8 larger subbundles. Codex should spend most effort in `src` and `tests`, not in `codex/bundles`.

Final closure is blocked unless:

```text
(src + tests changed lines) >= 5 × codex/bundles changed lines
```

Docs may be updated, but docs do not count as implementation in the ratio.

## Required validation at completion

- `dotnet build CanDoItAll.slnx --configuration Debug --no-restore`
- full unit test run
- focused integration matrix for template automation, process-mock runtime, runtime-host readback, scheduler/workflow launch, and process-driver boundary guards
- large-screen Playwright proof for project/project-structure process launch and run detail readback
- optional live OpenAI process-template smoke when explicit live env variables are present
- source scans for Process Core dependency drift, driver self-registration/reflection, fallback selector, mutation APIs, secret leakage, bundle-path coupling, and large-file growth

## Non-goals

- Do not approve execution-capable process drivers.
- Do not add driver self-registration, reflection discovery, fallback selector, or hidden manager/scheduler/workflow driver hooks.
- Do not move template/domain concepts into Process Core.
- Do not replace UI/browser proof with API-only proof for the user-facing launch flow.
