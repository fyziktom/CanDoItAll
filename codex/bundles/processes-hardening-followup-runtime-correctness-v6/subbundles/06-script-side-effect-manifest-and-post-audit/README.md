# SB06: Harden script execution beyond regex scanning.

## Objective

Harden script execution beyond regex scanning.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Introduce a governed script side-effect manifest format for `workspace_pwsh_run_script` and `workspace_python_run_file`.
- Require manifest for governed process script execution when step does not allow product mutation.
- Block encoded/nested/child scripts unless declared and inspected.
- Add post-execution diff/path audit for product target roots in non-mutating steps.
- Add red-team tests for PowerShell `[IO.File]::WriteAllText`, redirection, `cmd /c`, encoded command, and Python `Path.open('w')`.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Completion Notes

- Added typed `GovernedScriptSideEffectManifest` support for governed script tool calls.
- Required `sideEffectManifest` for non-mutating governed process scripts.
- Added red-team denials for static IO, redirection, undeclared shell delegation, encoded commands, and Python write APIs.
- Added runtime product-root snapshot auditing after non-mutating script execution.
- Focused unit validation passed: `bundle://proof/SB06/transcripts/passing.txt`.

## Covered Inputs

- RN04 allow script-based side effects through imperfect regex inspection.
- RQ05 script side-effect manifest.

## Prerequisites

- SB05 closure gate passes.
- Target grounding from SB01/SB04 remains trusted.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs

## Deliverables

- Declarative script side-effect manifest contract for governed script execution.
- Policy denial for non-mutating steps when script side effects are undeclared or unverifiable.
- Block/denial coverage for encoded commands, nested scripts, child scripts, redirection, shell delegation, and static IO APIs.
- Post-execution diff/path audit where runtime can observe product target roots.

## Dependency Impact

- SB07 depends on this analyzer before policy extraction.
- SB14 red-team closure depends on script bypass resistance across process types.

## Validation Depth

- Red-team unit tests for PowerShell `[IO.File]::WriteAllText`, redirection, `cmd /c`, encoded command, and Python `Path.open('w')`.
- Positive tests for declared no-mutation manifest and declared safe target paths.
- Anti-stub audit proving regex-only detection is not the only guard.

## Implementation Steps

- Define strongly typed side-effect manifest model or equivalent typed contract.
- Require manifest for governed script tools when product mutation is not allowed.
- Inspect declarations and script content before approval.
- Add post-run path diff audit where available.
- Record proof under `bundle://proof/SB06/`.

## Do Not Do

- Do not rely only on regex detection of known write commands.
- Do not silently allow nested, encoded, or child script execution in non-mutating steps.
- Do not treat software project paths specially.

## Acceptance Checklist

- Red-team script bypass tests are denied.
- Declared non-mutating scripts can run when targets are verified.
- Product mutation steps still allow declared product mutations through typed authority.
- Focused policy tests pass.

## Proof Required

- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB06/semantic-invariants.md`
- Red-team/failing-first transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB06 changes runtime policy only.

## Progression Gate

- Passed. SB07 may start after script side-effect manifest and red-team denial tests passed.

## Suggested Agent Prompt

- Implement SB06 typed script side-effect governance, update `proof/SB06`, run policy tests, and record gate closure before policy extraction.
