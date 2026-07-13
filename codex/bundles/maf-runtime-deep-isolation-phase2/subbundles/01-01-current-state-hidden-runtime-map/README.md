# SB01 Current-State Hidden Runtime Map

## Status

- `Ready`

## Objective

Create the implementation baseline: exact inventory of `MafAgentRuntime` partial files, private nested builders, hidden DTOs, runtime-owned helpers, tests that rely on runtime internals, and source scans that later architecture guards must enforce.

## Covered Inputs

- N001, N004, N005
- MAF2-R001, MAF2-R012

## Prerequisites

- None.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `bundle://analysis/01-current-state.md`
- `bundle://inventories/01-scope-inventory.md`

## Deliverables

- Refresh `inventories/01-scope-inventory.md` with current implementation-time line counts.
- Add a machine-readable nested-type inventory artifact under `evidence/SB01/`.
- Add baseline command transcripts for partial file and nested type scans.
- Identify every existing test that directly constructs `MafAgentRuntime`, calls static runtime helpers, or uses reflection against runtime internals.

## Dependency Impact

- SB02-SB08 depend on this inventory.
- If SB01 misses a hidden type, later architecture guards may falsely pass and the runtime can remain partially coupled.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof.

## Implementation Steps

1. Re-run the source scans from `inputs/01-source-artifacts.md`.
2. Add or update an inventory artifact with file, line count, nested type, current owner, proposed target owner, and owning subbundle.
3. Add a test dependency inventory for runtime static/helper usage.
4. Mark any intentionally allowed nested runtime type with a justification and expiration rule.
5. Update `reviews/01-execution-report.md` with SB01 evidence paths.

## Scope Exceptions

- Do not implement extraction in this phase.

## Do Not Do

- Do not rename or move production files.
- Do not weaken the inventory by grouping "other helpers" without naming them.
- Do not treat private nested classes as acceptable merely because they are small.

## Acceptance Checklist

- Every `MafAgentRuntime*.cs` file is listed.
- Every private nested class/record/enum under `MafAgentRuntime` is listed or explicitly justified.
- Every builder accepting `MafAgentRuntime owner` is listed.
- Every known runtime-static test dependency is listed.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Transcript for nested type scan.
- Transcript for test dependency scan.
- Anti-stub audit confirming no implementation changes were made in SB01.

## Browser Validation Logging

- N/A: backend architecture inventory only.

## Progression Gate

- SB02 may start only after the inventory and semantic gate are accepted.

## Suggested Agent Prompt

```text
Implement SB01 only. Produce the hidden runtime inventory and proof artifacts. Do not move code yet. Stop if any nested runtime type cannot be assigned to a target owner.
```
