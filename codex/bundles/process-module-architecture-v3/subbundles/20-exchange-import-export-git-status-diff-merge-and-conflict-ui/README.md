# SB20 Exchange, Import/Export, Git Status, Diff, Merge, And Conflict UI

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Implement process exchange import/export UI and generic Git UI components for status, diff, commit, merge, conflict display, and conflict resolution over the typed Git wrapper and template/versioning contracts.

## Covered Inputs

- REQ-034, REQ-037 to REQ-041, REQ-051, REQ-052.
- US-024, US-025, and US-055.
- AC-024, AC-026, AC-036, AC-039, AC-040.

## Prerequisites

- SB04 Git wrapper and template foundation complete.
- SB19 template component import metadata complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/architecture/09-template-git-versioning-and-migrations.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/10-security-governance-and-agent-change-auditing.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Process exchange import/export UI using `ProcessExchangeEnvelope`.
- Generic Git UI components for status, diff, commit, merge, and conflict resolution.
- Template global/local conflict display and manual resolution flow.
- Unauthorized mutation audit display suitable for manager/security workflows.

## Dependency Impact

- SB28 depends on Git conflict and unauthorized mutation proof.
- Future non-Process modules can reuse generic Git components.

## Validation Depth

- Git wrapper integration tests.
- Component tests for diff/conflict/status UI.
- Import/export envelope tests with warnings and source metadata.
- Playwright proof for conflict display and manual resolution.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Implementation Steps

1. Implement exchange import/export UI over application commands.
2. Build generic Git status and diff components outside Process-specific screens.
3. Build conflict display and resolution components using typed conflict records.
4. Wire template global/local conflict flows.
5. Wire unauthorized mutation audit projection display.
6. Add tests, scans, and Playwright proof.

## Do Not Do

- Do not implement Git semantics manually.
- Do not make Git UI components Process-specific.
- Do not expose sensitive diff content without authorization/redaction.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Import/export preserves metadata and warnings.
- [ ] Git status/diff/conflict components are generic.
- [ ] Conflict resolution flow is typed and testable.
- [ ] Unauthorized mutation audit is visible and redacted.
- [ ] Browser proof exists.

## Proof Required

- Git wrapper/component/integration test output.
- Playwright exchange/Git screenshot evidence.
- Security/redaction scan output.
- Story coverage table for US-024, US-025, and US-055.

## Browser Validation Logging

- Required. Capture exchange route/state, Git component actions, conflict resolution assertions, screenshot, and console/network summary.

## Progression Gate

- SB28 final security and Git regression may rely on this proof after all tests and scans pass.

## Suggested Agent Prompt

Execute SB20 from `codex/bundles/process-module-architecture-v3/subbundles/20-exchange-import-export-git-status-diff-merge-and-conflict-ui`. Implement exchange and generic Git UI over typed Git wrapper contracts only.

## Handoff Notes For Next Bundle

Record reusable Git component APIs and security restrictions for final regression.
