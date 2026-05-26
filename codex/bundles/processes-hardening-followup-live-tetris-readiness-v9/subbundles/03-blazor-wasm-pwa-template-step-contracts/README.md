# SB03: Blazor WASM PWA Template Step Contracts

## Status

- Status: `Completed`

## Objective

Harden generic Blazor WASM PWA template steps so architecture, implementation, validation, repair, writeback, and escalation have correct typed operation boundaries.

## Covered Inputs

- RQ03 generic template step boundaries.
- RQ09 template drift red-team checks.

## Prerequisites

- SB02 generic live-run profile separation is complete.

## Exact Source References

- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.md`
- `repo://Templates/Processes/processes/blazor-app-delivery/steps/resolve-blazor-contract.md`
- `repo://Templates/Processes/processes/blazor-app-delivery/steps/implement-blazor-change.md`
- `repo://Templates/Processes/processes/blazor-app-delivery/steps/validate-blazor-runtime.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Generic template text and sidecar docs that keep product mutation in implementation/repair only.
- Tests or source assertions proving validation/revalidation/writeback/escalation are not product-mutating.

## Dependency Impact

- SB04, SB08, SB10, and SB14 rely on these contracts to infer safe tools and proof requirements.

## Validation Depth

- Critical foundation. Require Semantic Adequacy Gate proof for mutation-boundary enforcement and topic-neutral template language.

## Implementation Steps

1. Audit the Blazor app delivery definition and step docs.
2. Remove app-topic-specific wording from reusable template instructions.
3. Confirm operation scopes and allowed operations match each step responsibility.
4. Update tests/source assertions.

## Do Not Do

- Do not add fallback mutation rights to validation, writeback, or escalation.
- Do not replace typed operation contracts with prose-only instructions.

## Acceptance Checklist

- Planning/contract step is read-only.
- Implementation and repair are the only product-mutating steps.
- Runtime/browser validation is read-only against product files.
- Writeback uses `ExecuteExternalAction`, not `MutateProductTarget`.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/transcripts/passing.txt`
- `proof/SB03/transcripts/source-assertions.txt`

## Browser Validation Logging

- N/A unless template changes surface in UI during this subbundle; UI verification is planned in SB07/SB14.

## Progression Gate

- SB04 may start only after typed operation boundaries and topic-neutral template assertions pass.

## Suggested Agent Prompt

Harden the generic Blazor WASM PWA template contracts and prove each step has the narrowest correct operation target scope.
