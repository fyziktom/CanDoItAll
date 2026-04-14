# Template helper isolation and pack immutability decision

## Status

- Prepared

## Objective

- Finish isolating duplicated template-to-editor mapping rules and make the pack-caching/immutability tradeoff explicit and safe.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateLibraryService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackLoader.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Targeted follow-up`

## Implementation Steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If the change introduces shared mutable cross-scope state or still leaves the same mapping rules duplicated in multiple services, stop and open the template-isolation corrective playbook.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested Agent Prompt

```text
Implement only subbundle 09-template-helper-isolation-and-pack-immutability-decision. Finish isolating duplicated template-to-editor mapping rules and make the pack-caching/immutability tradeoff explicit and safe. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Finish isolating duplicated template-to-editor mapping rules and make the pack-caching/immutability tradeoff explicit and safe.

### Required deliverables
- One shared owner for role-draft and artifact-expectation mapping rules instead of repeating that logic across catalog, library, and projection services.
- A short design note that explicitly chooses one of two safe directions: keep the pack scoped because the graph is mutable, or make the pack truly immutable and safe for broader caching.
- No introduction of a shared mutable singleton pack graph without immutability or defensive cloning.
- Regression proof that catalog/library/projection behavior still agrees after helper extraction.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests" -v:minimal`

### Review questions
1. Do role/artifact mapping rules now have one clear owner?
2. Is the pack caching/immutability decision now explicit, justified, and thread-safe?
3. Did helper isolation reduce duplication without widening into unrelated template rewrites?

### Corrective trigger
If the change introduces shared mutable cross-scope state or still leaves the same mapping rules duplicated in multiple services, stop and open the template-isolation corrective playbook.

### Corrective template
- `subbundles/_corrective-template-isolation-reset`

### Detailed execution notes
- The current scoped loader avoids a shared mutable graph, so do not 'optimize' it into a singleton unless the graph becomes safely immutable.

