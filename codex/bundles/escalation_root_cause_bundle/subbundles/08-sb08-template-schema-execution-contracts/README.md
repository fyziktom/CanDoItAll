# SB08 - Template Schema Execution Contracts

## Status

- `Completed`
- Critical foundation: yes

## Objective

Add typed process template schema contracts for step execution class, tool plans, subprocess contracts, required receipts, produced artifact slots, no-go outputs, and branch decisions. Template validation must fail when hard runtime behavior exists only in markdown prose.

## Covered Inputs

- GPTPro template/agent contract hardening finding.
- REQ-011, REQ-012, REQ-015, REQ-016, REQ-017, REQ-018, REQ-020.
- User requirement to cover all process templates and artifact templates.

## Prerequisites

- SB01 resolved placeholder rules complete.
- SB02 aggregate gate semantics complete.
- SB06 child bridge semantics complete.
- SB07 tool-plan guard semantics complete.

## Exact Source References

- `bundle://codex/08-template-agent-contract-hardening.md`
- `bundle://templates/01-template-audit-index.md`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateCompatibilityScanner.Json.cs`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessSubprocessContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessContractVersions.cs`
- `repo://Templates/Processes/README.md`
- `repo://Templates/Processes/manifest.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateCompatibilityHistoryTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessTemplateRuntimeWritebackTextTests.cs`

## Deliverables

- Typed execution class values: `AgentReasoningOnly`, `AgentWithToolPlanGuard`, `DeterministicToolPlan`, `RuntimeOwnedSubprocess`, `BranchDecision`.
- Schema support for deterministic tool plan metadata and required receipt metadata.
- Schema support for subprocess contract metadata, accepted child outputs, repaired outputs, no-go outputs, and produced artifact slots.
- Template validation failures for missing typed metadata where hard gates are detected.
- Compatibility/migration rules for existing templates.

## Dependency Impact

- SB09 cannot migrate templates safely until schema and validators exist.
- SB10 uses execution class and capability metadata.
- SB11 consumes deterministic tool plan metadata for runtime-owned execution.

## Validation Depth

- Critical foundation with schema tests, template fixture tests, and compatibility scanner tests.
- Semantic proof must show validation rejects prose-only hard gates.

## Implementation Steps

1. Add or extend process template contract records for execution class and typed gate metadata.
2. Add strongly typed constants/enums for execution classes and branch/no-go identifiers.
3. Extend template JSON loader to read the new fields with clear diagnostics.
4. Extend compatibility scanner to identify prose-only hard gates and missing typed metadata.
5. Add validation for deterministic tool plans, required receipts, subprocess contracts, accepted child outputs, no-go outputs, and produced slots.
6. Add migration compatibility behavior for templates not yet migrated in SB09 while still allowing strict validation mode.
7. Add tests for each execution class.
8. Add tests that fail when a template mentions required receipts or runtime-owned subprocess in prose without typed metadata.
9. Add tests for invalid branch identifiers and missing produced artifact slots.
10. Document schema expectations in the template audit checklist.

## Do Not Do

- Do not migrate all templates before validators exist.
- Do not encode execution class as arbitrary strings without central values.
- Do not parse markdown prose at runtime as the authoritative hard gate.
- Do not loosen compatibility so invalid templates silently pass strict validation.

## Acceptance Checklist

- [x] Execution class is typed and validated.
- [x] Deterministic tool plan metadata is typed and validated.
- [x] Required receipt metadata is typed and validated.
- [x] Subprocess contract metadata is typed and validated.
- [x] Prose-only hard gates fail strict validation.
- [x] Existing templates can be migrated in SB09 with clear diagnostics.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- Failing-first schema validation tests.
- Passing template loader/scanner tests.
- Source assertions for new typed records and validators.
- Production Behavior Artifact Matrix if new schema records are introduced.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB09 may start only after strict template validation can detect missing typed execution/tool/subprocess/artifact metadata.

## C# Architecture Impact

Adds template contract surface and validators that runtime can consume without markdown parsing.

## Boundary Ownership

`Processes.Templates` owns loading/validation. `Processes.Contracts` owns shared records only when runtime/application consumers require them.

## Dependency Direction

Template schema changes must not make runtime depend on template implementation details.

## Pattern Decision

Use PSR-008: focused validators over typed template records.

## Testability Contract

Schema validation tests must use real template fixtures and isolated invalid fixtures.

## Partial Class Policy

No partial-class changes expected.

## Architecture Proof Required

- Contract placement rationale.
- Strict validation negative tests.
- Dependency/cycle evidence if shared contracts change.

## Suggested Agent Prompt

```text
Execute SB08 only. Add typed template execution contracts and strict validation for tool plans, receipts, subprocess outputs, artifact slots, and branch decisions. Do not migrate all templates yet except minimal fixtures needed for tests.
```
