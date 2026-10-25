# 13 · Phased plan, prerequisites and stopping boundaries

## Planning contract

The phases below are architectural envelopes, not fixed implementation bundles or time estimates. Each phase is decomposed after refreshing the then-current branch. Decisions remain proposed until reviewed. Tests listed in this package are planned cases, not existing passing tests.

The phase register and individual cards define scope, exclusions, entry/exit evidence, performance scenarios and residual risk. No phase is complete merely because files compile. Conversely, a full unfiltered stable suite is not run after every small edit; repository guidance determines the named broader invalidation and closure triggers (SRC-12/SRC-16).

## Dependency map

```text
P00 evidence -> P01 foundations -> P02 Projects closed vertical
                                     |             |
                                     v             v
                               P03 intentions    P05 owner waves
                                     |             |
                                     v             |
                               P04 basis pilot ----+
                                     |             |
                                     +------> P06 selected external effects
                                                   |
                                           P07 workflows/processes
                                                   |
                              selected waves ----> P08 release closure
```

P05 may run alongside P03/P04 on non-overlapping owners. A P05 slice that adopts decision-basis behavior additionally depends on P04. P06 requires the relevant P05 slice, not completion of every possible owner in the product. P07 is intentionally late because process/checkpoint/governance integration has the largest blast radius.

## What can ship independently

P00 creates useful evidence but changes no guarantees. A compatible P01 cleanup or metric improvement can ship independently. P02 can ship once **all enabled Projects callers** are converted and the declared search/outcome/lifecycle contract is complete; the rest of the application remains explicitly unconverted. P03 improves that pilot's usability. P04 covers one named governed operation, not all agent tools. Each P05 owner wave has its own closure. Later storage/process slices carry their own declared boundaries.

Do not wait for the full architecture to be implemented before protecting a known lost-update path. Do not ship an incomplete first owner with a fallback that silently fills missing tokens. When a caller is not ready, disable that route for protected writes with clear compatibility information rather than silently downgrade the contract.

## Phase scopes and responsibilities

| Phase | Primary review owner | Supporting reviewers | Typical code/test impact |
|---|---|---|---|
| P00 | Architecture/Projects owner | UI extraction, runtime, persistence, QA | Inventories, source anchors, current test discovery, baseline harness |
| P01 | Persistence and Projects owner | Runtime authority, API, migration | Small utilities/ports, error classification, cleanup/lock tests |
| P02 | Projects owner | Workbench tools, UI/API, Search, migration/transfer | Canonical schema, owner save/read, all callers, draft conflict and replay proof |
| P03 | Projects and UI owner | Tool/API contracts | Narrow intentions, row identity, merge policy and renderer journeys |
| P04 | Workbench planning/runtime owner | Projects, governance, operator UX | Trusted basis capture, publication checks, approval/takeover/replanning |
| P05 | Selected fact owner per wave | Composition/UI/backend boundary reviewers | Owner-specific stamps/transitions, contracts and matching tests |
| P06 | Storage/external-effect owner | Governance, projections, operations | Existing intent/recovery protocols, capability matrix, uncertainty/compensation |
| P07 | Processes/Workflow owner | Runtime, Workbench, UI, migration | Definition/run separation, resume/replay, approvals, claim fences |
| P08 | Release/architecture owner | All changed owners and operations | Cross-slice reconciliation, lifecycle rehearsals, required canonical lanes |

These are roles to assign, not invented people or commitments of staffing.

## Every implementation slice requires

A stable logical mutation ID; exact then-current source/sibling evidence; covered and excluded entry points; approved invariants; transport/schema compatibility plan; deterministic failure/race cases; discovered test counts; expected extra SQL/locks/payload; actual observed measurements; supported rollback/stop state; and named remaining risks.

Keep schema + owner + enabled caller conversion together where they enforce one boundary. Pure renderer extraction and mutation semantics can be separate commits, but must integrate against the same accepted contract before validation. Do not mix unrelated provider runtime or scheduler fixes into the concurrency pilot.

## Broad-suite trigger policy

Use focused owner tests continuously. Use a frozen checkpoint and the documented broad stable/platform gates after a foundation change with repository-wide impact, and for release/merge closure. UI shipping behavior requires real browser proof. Process/provider/live/Linux-specific lanes are separate and must be executed when the changed behavior depends on them; neither an unrelated green stable lane nor fake provider tests substitute for them.

The exact current commands and filters are resolved from the current repository, not hard-coded from historical names in this ZIP. A required environment that is unavailable blocks that claim and is reported as such.
