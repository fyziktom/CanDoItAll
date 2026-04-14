# Corrective playbook — lifecycle reset

## Status

- Blocked

## Objective

- Preserve this corrective playbook so a failed gate can be repaired before downstream work resumes.

## Covered Inputs

- The failed gate, stop rule, or blocking defect that triggered this corrective playbook.

## Prerequisites

- A numbered subbundle or architecture gate has failed, and downstream work is blocked.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs

## Dependency Impact

- All downstream work remains blocked until this corrective playbook is completed and the failed gate is rerun successfully.

## Validation Depth

- Corrective gate

## Implementation Steps

1. Capture the exact failing gate, stop rule, or proof artifact before changing code.
2. Apply the smallest repair that removes the blocking defect at the real ownership boundary.
3. Rerun the failed proof and update the live execution report and gate memo while the evidence is fresh.

## Scope Exceptions

- Do not continue downstream implementation while this corrective playbook is open.

## Do Not Do

- Do not weaken the invariant or proof requirement just because the current implementation prefers looser behavior.
- Do not close the corrective path on prose alone.

## Acceptance Checklist

- The failed gate has a real repair.
- Fresh proof exists.
- Downstream work is still blocked until the gate is rerun and passes.

## Proof Required

- The exact failing command, artifact, or gate question that triggered the corrective path.
- Fresh rerun proof after the corrective implementation lands.

## Browser Validation Logging

- N/A unless the corrective work changes visible /processes behavior. If it does, capture fresh Playwright proof before reopening the gate.

## Progression Gate

- Downstream work may resume only after the corrective subbundle is completed and the failed gate passes on fresh proof.

## Suggested Agent Prompt

`	ext
Execute only corrective subbundle _corrective-lifecycle-reset. Repair the specific blocking defect, rerun the failed proof on fresh artifacts, update the live execution report and gate memo, and do not unblock downstream work until the gate passes.
`

## Preserved Bundle Notes

# Corrective playbook — lifecycle reset

Invoke this when lifecycle singularity or allocator safety still depends on ordering logic.

## Trigger examples

- more than one draft/published row can still exist per definition;
- `ActivePublishedVersionId` is still weakly protected;
- `MAX + 1` version allocation remains.

## Mandatory repair moves

- move lifecycle assumptions into DB-backed invariants;
- replace weak allocators;
- rerun publish/save/start-run proof before reopening Gate C.



