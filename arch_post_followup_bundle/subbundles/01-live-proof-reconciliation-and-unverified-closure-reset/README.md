# Live proof reconciliation and unverified-closure reset

## Status

- Prepared

## Objective

- Reconcile the prior closure claim with the live repository, capture the newly reopened findings from evidence, and reset the proof record before any more code changes continue.

## Covered inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact source references

- architecture_followup_bundle/reviews/01-execution-report.md
- architecture_followup_bundle/reviews/02-architecture-gate-memo-log.md
- .codex-test-results/integration
- .codex-test-results/components
- .codex-test-results/mcp-processes
- tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- tests/CanDoItAll.Tests.Integration/ProcessSchemaIntegrationTests.cs
- tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs
- tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs

## Dependency impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation depth

- `Critical foundation`

## Implementation steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do not do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If the proof record still overclaims or cannot be reconciled from live evidence, stop immediately and open the proof corrective playbook before touching production code.

## Acceptance checklist

- Satisfy the deliverables and review questions preserved below.

## Proof required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser validation logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested agent prompt

```text
Implement only subbundle 01-live-proof-reconciliation-and-unverified-closure-reset. Reconcile the prior closure claim with the live repository, capture the newly reopened findings from evidence, and reset the proof record before any more code changes continue. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved bundle notes

### Purpose
Reconcile the prior closure claim with the live repository, capture the newly reopened findings from evidence, and reset the proof record before any more code changes continue.

### Required deliverables
- A written proof-reconciliation memo that compares the claimed closure state with the current live code and test artifacts.
- Fresh `.trx` artifacts for the Process integration/component surface that this follow-up depends on.
- An updated execution report section that explicitly reopens the still-active gaps instead of inheriting the old closure claim.
- No production architecture change beyond proof capture and any narrowly necessary test scaffolding.

### Repository touchpoints
- `architecture_followup_bundle/reviews/01-execution-report.md`
- `architecture_followup_bundle/reviews/02-architecture-gate-memo-log.md`
- `.codex-test-results/integration`
- `.codex-test-results/components`
- `.codex-test-results/mcp-processes`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessSchemaIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessSchemaIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`

### Review questions
1. Does the fresh proof actually cover the Process surfaces that the previous closure claim depended on?
2. Does the reopened gap log now match the live source instead of the older execution report narrative?
3. Have all downstream phases been blocked from borrowing trust from the old closure claim?

### Corrective trigger
If the proof record still overclaims or cannot be reconciled from live evidence, stop immediately and open the proof corrective playbook before touching production code.

### Corrective template
- `subbundles/_corrective-proof-reset`

### Detailed execution notes
- This phase is deliberately allowed to reopen findings that were previously marked closed too early.
- Do not bury disagreement between the previous report and the live source; make it explicit in the report.
