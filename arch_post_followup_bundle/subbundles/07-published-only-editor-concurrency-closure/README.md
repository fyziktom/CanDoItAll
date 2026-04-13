# Published-only editor concurrency closure

## Status

- Prepared

## Objective

- Close the remaining stale-save hole in the published-only editor path by ensuring definition-level concurrency metadata is always present when editing an existing definition without a working draft.

## Covered inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact source references

- src/CanDoItAll.Modules.Processes/ProcessesService.cs
- src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs
- src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs
- tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Dependency impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation depth

- `High-value follow-up`

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
- If any existing-definition editor path still returns a null definition token, fail and reopen this subbundle before continuing.

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
Implement only subbundle 07-published-only-editor-concurrency-closure. Close the remaining stale-save hole in the published-only editor path by ensuring definition-level concurrency metadata is always present when editing an existing definition without a working draft. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved bundle notes

### Purpose
Close the remaining stale-save hole in the published-only editor path by ensuring definition-level concurrency metadata is always present when editing an existing definition without a working draft.

### Required deliverables
- An editor-loading path that always sets `DefinitionConcurrencyToken` when an existing definition is loaded, even if no working draft exists yet.
- A direct integration test that simulates a published-only or no-draft state and proves a stale save is rejected.
- No weakening of the current working-draft concurrency behavior.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`

### Review questions
1. Can an editor for an existing definition still be returned without a definition concurrency token?
2. Does a stale save now fail in the published-only/no-draft path instead of silently creating a new draft from stale state?
3. Did the change avoid regressing the normal working-draft concurrency flow?

### Corrective trigger
If any existing-definition editor path still returns a null definition token, fail and reopen this subbundle before continuing.

### Corrective template
- `subbundles/_corrective-template`

### Detailed execution notes
- This is a smaller gap than the older red blockers, but it is still a correctness hole and should be closed before final sign-off.
