# SB08: Workflow Evaluation And Process Workflow Bridge

## Status

- Completed

## Objective

Use MAF 1.6 workflow evaluation expected output concepts for deterministic process workflow tests where available.

## Covered Inputs

- RQ03: workflow evaluation expected output/ground truth adoption.

## Prerequisites

- SB02 adoption matrix must classify workflow evaluation support.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs

## Deliverables

- Workflow-backed process step regression tests using expected outputs or explicit compatibility fixtures.
- Artifact mapping proof from workflow output to process artifact expectation.

## Dependency Impact

- SB15 generic process regression and SB18 workflow mismatch red-team depend on this bridge.

## Validation Depth

- Critical semantic proof must show output mismatch causes process-owned artifact validation failure.

## Implementation Steps

- Audit workflow evaluation APIs in local packages.
- Add deterministic process workflow tests.
- Ensure workflows remain executors under Processes, not parallel process state machines.
- Update `proof/SB08`.

## Do Not Do

- Do not duplicate process lifecycle state inside workflow runtime.
- Do not accept workflow output without explicit artifact mapping fields.

## Acceptance Checklist

- Expected-output/ground-truth strategy is explicit.
- Positive mapping and negative mismatch tests exist.
- Proof artifacts are updated.

## Proof Required

- Failing-first mismatch transcript.
- Passing workflow bridge transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB15 may start only after workflow bridge semantics are proven or explicitly deferred.

## Suggested Agent Prompt

Wire deterministic workflow expected-output proof into process workflow tests without turning Workflows into a separate process state machine.

## Closure Proof

- bundle://proof/SB08/manifest.md
- bundle://proof/SB08/semantic-invariants.md

