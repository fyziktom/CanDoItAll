# Structured Input

## Core Objective

- Make the default 5032 multiteam .NET development flow reliable for a simple Calculator app E2E run.

## Success Criteria

- The prior failing run path is diagnosed from API/DB/artifacts, not inferred.
- Process templates make role boundaries explicit and enforceable.
- HR/readiness detects missing step operations or tool capabilities before execution.
- Updated templates are loaded by the development runtime.
- A fresh real process run does not repeat the false escalation loop.

## Hard Constraints

- Architects must not mutate product files.
- Implementation and repair lanes must be the only product-mutable lanes.
- QA can validate and capture proof but cannot write product code.
- Subprocess boundaries must stay small and testable.

## Allowed Side Effects

- Process template JSON and step prompt docs may change.
- Process runtime readiness/matching code may change.
- Focused unit/integration tests may be added or updated.
- Development DB runtime may be restarted and test process runs may be cancelled/cleaned as needed for proof.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- Live 5032 API and PostgreSQL process runtime tables.
- Process run artifacts under the scoped organization artifact folder.

## Input Coverage Signals

- False escalation in simple development.
- Need to block tools for roles that should not perform actions.
- Need to allow missing tools where the process step legitimately needs them.
- Need smaller subprocesses.
- HR matching should prevent most tool/capability mismatches.
- Need real-run validation, not only code review.

## Dependency And Sequencing Signals

- Live diagnosis must precede template/runtime changes.
- Template contract repair and HR/readiness guardrails must precede real-run proof.
- Real-run proof must run after rebuild, restart, and template reload.

## Validation Expectations

- Bundle validator passes after preparation and after closure updates.
- Targeted tests cover template contract invariants and HR/readiness failure modes.
- Full solution build passes.
- 5032 runtime starts against the development database.
- Fresh Calculator process run demonstrates the fixed route or records a concrete external blocker.

## Evidence Contract

- SQL/API snippets for current failing runs.
- Git diff of template/runtime/test changes.
- `dotnet test` or equivalent targeted test output.
- Solution build output.
- 5032 runtime/template reload output.
- Fresh process run id and step status table.

## UI Validation Strategy

- The runtime repair itself is not UI-visible.
- If the fresh Calculator run reaches browser/UI validation, capture the target route, viewport, screenshot path, and result in SB04.

## Browser Validation Analytics

- N/A until SB04 creates or validates the Calculator UI. SB04 must fill route, viewport, Playwright/browser actions, assertions, screenshot paths, and result when UI proof is reached.

## Working Assumptions

- Template files under `Templates/Processes` are the development runtime source of truth after restart/template reload.
- The current escalation is caused by a process contract and readiness gap, not by the Calculator requirements being inherently ambiguous.

## Primary Risks

- Over-correcting contracts could block legitimate subprocess launches.
- Under-correcting HR/readiness could leave the same bug latent in another role/process.
- Real process proof can be slowed by provider or environment state unrelated to this fix.
