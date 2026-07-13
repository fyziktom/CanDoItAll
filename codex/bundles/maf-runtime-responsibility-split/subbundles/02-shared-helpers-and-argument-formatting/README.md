# Shared Helpers And Argument Formatting

## Status

- `Completed`

## Objective

- Extract stable hashing and argument formatting out of `MafAgentRuntime` into correctly scoped helper classes without changing diagnostic output.

## Covered Inputs

- N004, N005
- Requirements R04, R05, R09, R10

## Prerequisites

- SB01 closure gate passed.
- Exact current hash and argument-formatting outputs are known from characterization proof.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessPlanHasher.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateHashing.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs`

## Deliverables

- Shared stable hashing helper in a foundation location only if dependency direction is clean.
- MAF-specific argument formatter helper inside the MAF project.
- Updated MAF runtime call sites delegating to helpers.
- Direct tests for full hash, short display hash, truncation, primitive values, JSON values, dictionaries, arrays, and null values.

## Dependency Impact

- SB03-SB07 should consume the extracted helpers where relevant. If helper extraction changes output, finalizer and repeated-tool diagnostics can drift.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Lock current output with failing-first or characterization tests.
2. Review existing process hashers before choosing shared helper API names and formats.
3. Create strongly named helper methods. Avoid one vague `Helpers` class.
4. Move MAF-only formatting to a MAF-specific helper.
5. Update runtime call sites.
6. Run focused tests and dependency-direction scans.

## Scope Exceptions

- Do not force existing process hashers to use the new shared helper unless that is a small, proven compatibility-preserving cleanup.

## Do Not Do

- Do not put MAF-specific formatting in `CanDoItAll.SharedKernel`.
- Do not change hash casing, prefix, length, or truncation semantics without explicit tests and documented acceptance.
- Do not add fallback hash behavior.

## Acceptance Checklist

- `ComputeStableHash` no longer lives in `MafAgentRuntime`.
- `FormatArgumentValue` no longer lives in `MafAgentRuntime`.
- Shared helper placement has no invalid project references.
- Existing and new tests prove output compatibility.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- Failing-first or characterization transcript for current outputs.
- Passing unit-test transcript.
- Dependency-direction scan transcript.
- Source assertions proving MAF delegates to helper classes.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- If this subbundle introduces a production signal, state, record, or event, add a Production Behavior Artifact Matrix to both proof artifacts.

## Browser Validation Logging

- N/A. No browser-visible behavior should change in this helper subbundle.

## Progression Gate

- SB03-SB07 may start only after helper output compatibility and dependency direction are proven.

## Suggested Agent Prompt

```text
Implement SB02 only. Extract stable hashing and MAF argument formatting into focused helpers, preserve exact output where required, prove dependency direction, capture proof under proof/SB02, and stop if helper placement would create an invalid shared dependency.
```
