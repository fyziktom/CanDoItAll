# C# Testability Plan

## Characterization Tests

Before behavior changes, add or identify failing/characterization tests for:

- observation reader hides a blocked step when run-level `TakePerRun` truncates older step runs;
- operator action has runtime receipt but no AgentFramework observation and currently produces generic summary;
- finalization downgrades result but ledger still sees original produced artifacts;
- `prepare-solution-skeleton` child completed with `setup-handoff-after-repair` is not machine-readable as accepted metadata;
- child folder-only evidence can pass generic evidence resolution when accepted child output is absent.

## Isolated Unit Tests

| Extracted owner | Required direct tests |
| --- | --- |
| Observation selector/query | exact run+step query, run-level fallback only for dashboard enrichment. |
| Blocked packet builder | missing AF observation, missing produced artifact, child active, child no-go, missing tool, unknown diagnostic. |
| Result-summary projector | completed, blocked, waiting approval, validation failure, failed tool receipts. |
| Subprocess contract parser/validator | accepted/no-go arrays, manual skip output policy, parent expectation exists, no accepted/no-go overlap. |
| Parent subprocess bridge | no child, child active, accepted handoff, repaired accepted handoff, no-go escalation, completed without accepted output, infrastructure failure. |
| Artifact descriptor resolver/materializer | descriptor rendering, primary ref, content hash stability, missing readback failure. |
| Tool preflight | missing provider, not composed, denied scope, missing agent capability, available tool. |

## Negative Tests

- A child run with only a steps folder and no accepted handoff artifact must not complete a parent.
- `setup-repair-escalation` must not be accepted as `solution-skeleton-evidence`.
- A required output manual skip without typed already-satisfied proof must fail template validation.
- A missing `project_structure_process_subprocess_launch` provider must block before agent execution.
- A result downgraded to `NeedsManager` must not create produced artifact ledger events from the original success.
- A produced artifact hash based only on raw model output must fail content-grounding assertions.

## Composition And Integration Smoke

- Process dispatch smoke with fake preflight pass proving normal agent path still runs.
- Runtime-owned subprocess smoke proving adapter invokes bridge and bypasses `ExecuteRunAsync`.
- Template pack load/validation smoke over all `Templates/Processes/processes` definitions.
- Projection smoke proving operator action can render blocked packet categories.

## Proof Requirements

Every critical subbundle must write transcripts under `proof/SBxx/transcripts/` and update `proof/SBxx/manifest.md` plus `proof/SBxx/semantic-invariants.md`.

Proof must include:

- failing-first transcript for behavior-changing tests;
- passing transcript for the same behavior after implementation;
- source assertion that moved behavior lives in focused services;
- anti-stub audit for production TODO/NotImplemented/template-only output;
- CodeAnalytics refresh for dependency or large-class changes;
- old-class shrink or thin-facade proof for extracted logic.

## Test Seam Rule

Tests for extracted behavior must instantiate the extracted service directly with fake dependencies. They must not require full Blazor app host, live LLM/provider, network, database, or external process unless explicitly marked integration smoke.
