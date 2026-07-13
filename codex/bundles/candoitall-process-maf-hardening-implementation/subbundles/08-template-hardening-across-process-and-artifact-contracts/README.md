# SB08 - Template Hardening Across Process And Artifact Contracts

## Status

- `Completed`
- Critical foundation: yes

## Objective

Apply typed hardening to every affected process template and artifact contract so all hard subprocess, artifact, receipt, skip, branch, and completion gates are machine-readable.

## Covered Inputs

- F09, F11 plus local audit of all subprocess parents and shared artifact templates.
- R09, R11, R12, R13.
- GPTPro B06.

## Prerequisites

- SB04 typed contract loader/validation complete.
- SB05 bridge behavior complete.
- SB06 descriptors/materialization complete.
- SB07 exact preflight complete.

## Exact Source References

- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/steps/prepare-solution-skeleton.md`
- `repo://Templates/Processes/processes/dotnet-development-slice/steps/implement-code-change.md`
- `repo://Templates/Processes/processes/dotnet-development-slice/steps/slice-repair-code-change.md`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/steps/architecture-review.md`
- `repo://Templates/Processes/processes/software-delivery/steps/implementation.md`
- `repo://Templates/Processes/processes/software-delivery/steps/capture-ui-screenshots.md`
- `repo://Templates/Processes/processes/software-delivery/steps/capture-ui-screenshots-after-repair.md`
- `repo://Templates/Processes/processes/software-delivery/steps/record-runtime-commands.md`
- `repo://Templates/Processes/processes/software-delivery/steps/record-runtime-commands-after-repair.md`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`
- `repo://Templates/Processes/processes/dotnet-architecture-design-review/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json`
- `repo://Templates/Processes/shared/artifacts`

## Deliverables

- Typed `SubprocessContract` metadata for all nine subprocess parent steps or explicit exception rows.
- `prepare-solution-skeleton` accepts initial and repaired setup handoff and rejects setup repair escalation as no-go.
- `prepare-solution-skeleton` manual skip disabled or converted to typed already-satisfied output that materializes parent evidence.
- Feature/slice/software-delivery parents get accepted/repaired/no-go metadata as applicable.
- Screenshot and runtime-command parents get required child receipt metadata.
- Long hard-gate prose is shortened or mirrored into typed `CompletionGates`, `RequiredReceipts`, `RequiredPaths`, `RequiredFileContentChecks`, and `BranchRules`.
- Shared artifact templates in scope have typed hard gates or explicit follow-up exceptions.
- Template loader validates the full template set.

## Dependency Impact

- SB09 final regression and closure depends on all template changes. Missing one affected template can reproduce the failure class.

## Validation Depth

- Critical foundation with semantic adequacy gate.

## Implementation Steps

1. Start from `inventories/02-subprocess-contract-inventory.md`.
2. Update process definitions with typed metadata for SP01-SP09.
3. Adjust step markdown to remove instructions that normal agents must launch runtime-owned controlled subprocesses.
4. Move hard gates from prose into typed fields where loader supports them.
5. Audit shared artifact templates and add typed validation metadata or exception rows.
6. Run template pack load and validation over all process templates.
7. Add regression tests for each parent contract, manual skip policy, and representative artifact hard gate.

## Scope Exceptions

- It is acceptable to leave explanatory markdown if the hard gate is also typed.
- Shared artifacts with no hard completion semantics can be listed as inspected/no change.

## Do Not Do

- Do not edit only `prepare-solution-skeleton`.
- Do not leave accepted repaired outputs in prose only.
- Do not allow manual skip to bypass required parent evidence.
- Do not encode branch outcomes as unvalidated strings scattered through runtime code.

## Acceptance Checklist

- [ ] SP01-SP09 have typed metadata or explicit exception rows.
- [ ] `prepare-solution-skeleton` no longer has unsafe manual skip.
- [ ] Parent docs no longer tell normal agents to launch runtime-owned subprocesses as the primary path.
- [ ] Template validation fails for missing typed subprocess contract.
- [ ] Template validation fails for required-output manual skip without output proof.
- [ ] Shared artifact hard-gate audit is complete.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- Failing-first template validation tests.
- Passing validation across all process templates.
- Source assertions for changed template metadata.
- Changed-file hashes for template files.
- Anti-stub audit.
- Production Behavior Artifact Matrix for typed template contracts and materialization metadata.

## Browser Validation Logging

- `N/A`.

## Progression Gate

- SB09 may start only after all affected templates validate and every exception row is explicit.

## C# Architecture Impact

Template data migration plus validation behavior.

## Boundary Ownership

Template JSON owns typed metadata; loader/validator owns structural correctness; runtime consumes normalized contracts.

## Dependency Direction

Template changes must not force runtime to parse markdown.

## Pattern Decision

No new production pattern unless validator grows; if it does, use focused validator classes.

## Testability Contract

Tests load real template fixtures and assert typed metadata, not only file existence.

## Partial Class Policy

No partial-class changes expected unless loader implementation already uses partials; if touched, keep edits focused.

## Architecture Proof Required

- Template validation proof.
- Source assertion that runtime no longer relies on prose for hard gates.

## Suggested Agent Prompt

```text
Execute SB08 only. Apply typed subprocess/artifact hardening across all affected templates and shared artifact contracts. Do not narrow scope to the blocked example. Run full template validation and update proof.
```
