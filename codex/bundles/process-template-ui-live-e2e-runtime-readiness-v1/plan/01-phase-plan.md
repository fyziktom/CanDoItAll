# Phase Plan

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Baseline + code-first gate]
  SB02[SB02 UI/project/project-structure launch proof]
  SB03[SB03 Blazor/.NET automation hardening]
  SB04[SB04 Multi-team software-delivery automation]
  SB05[SB05 PostgreSQL business-analysis automation]
  SB06[SB06 Runtime-host readback on real runs]
  SB07[SB07 Scheduler/workflow launch + read-only verification jobs]
  SB08[SB08 Release matrix + final red-team]
  SB01 --> SB02 --> SB03 --> SB04 --> SB05 --> SB06 --> SB07 --> SB08
```

## Critical Subbundles

All subbundles are critical and intentionally larger implementation areas. The implementation agent must not split these into proof-only micro edits.

## Phase Gates

Each subbundle must include:

- real `src` or `tests` changes unless explicitly closed as a pure validation subbundle,
- focused tests or browser proof,
- source scans for Core leakage and forbidden runtime-host side effects,
- concise proof in the execution report,
- downstream impact decision.

## Final code-first ratio gate

Final closure is blocked unless:

```text
(src + tests changed lines) >= 5 × codex/bundles changed lines
```

Docs do not count as implementation. Screenshots/proofs do not count as implementation.

## Final validation matrix

- Build: `dotnet build CanDoItAll.slnx --configuration Debug --no-restore`
- Unit: full unit project
- Integration: representative template automation, business-analysis PostgreSQL automation, runtime-host readback, scheduler/workflow read-only job lifecycle
- Playwright: large-screen UI launch and run detail readback
- Optional live: one bounded OpenAI process-template smoke if explicit env variables are present
- Source scans: Core dependency drift, driver self-registration/reflection, fallback selector, mutation APIs, secret leakage, bundle-path coupling, large-file growth
