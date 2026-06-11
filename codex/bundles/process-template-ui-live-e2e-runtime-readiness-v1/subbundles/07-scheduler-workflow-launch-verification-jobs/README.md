# SB07: Scheduler/workflow launch + read-only verification jobs

## Status
- Status: Completed

## Objective
Prove scheduler/workflow-origin process launch and read-only verification job lifecycle without direct driver hooks.

## Covered Inputs
- Raw request: continue toward generic runtime host without unsafe side effects.
- REQ-007: prove scheduler/workflow-origin process launch and read-only verification job execution.

## Prerequisites
- SB06 closure gate proves readback against real run and step ids through manager/facade boundaries.
- SchedulerPlanner and workflow-origin integration source paths are available.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs
- repo://src/CanDoItAll.Modules.SchedulerPlanner
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://tests/CanDoItAll.Tests.Integration

## Deliverables
- Add/strengthen scheduler-origin template launch test for a representative process.
- Add/strengthen workflow-origin launch test or workflow-origin trigger proof.
- Add read-only verification job lifecycle result with source kind/reference/correlation id/start/end/readback status.
- Verify scheduler/workflow paths use process service/facade, not driver execution hooks.

## Dependency Impact
- SB08 final red-team cannot pass if scheduler/workflow launch bypasses process-owned services or introduces direct driver hooks.
- Future runtime-host work depends on this subbundle keeping verification jobs read-only.

## Validation Depth
- Run focused scheduler-origin, workflow-origin, and read-only verification job lifecycle integration tests.
- Scan scheduler/workflow/process integration for forbidden driver hook patterns and mutation APIs.
- Include semantic adequacy proof, manifest, positive/negative transcripts, source assertions, and anti-stub audit under `proof/SB07/`.

## Implementation Steps
- Audit scheduler and workflow launch paths around `StartRunFromTriggerAsync` or equivalent process-owned entry points.
- Add or strengthen scheduler-origin and workflow-origin representative template launch tests.
- Add read-only verification job lifecycle assertions for source kind/reference, correlation id, start/end, readback status, and no driver mutation hook.
- Capture transcripts and forbidden-hook scans.

## Do Not Do
- Do not add scheduler/workflow driver runtime hooks.
- Do not call domain drivers directly from scheduler/workflow.
- Do not add hosted execution-capable driver worker.

## Acceptance Checklist
- Scheduler-origin run starts through `StartRunFromTriggerAsync` or equivalent process-owned path.
- Workflow-origin run starts through process-owned path.
- Read-only verification job runner records lifecycle and readback.
- No driver mutation hooks are introduced.

## Proof Required
- Focused integration transcript.
- Source scan for forbidden scheduler/workflow driver hook patterns.

## Completion Proof
- Manifest: `bundle://proof/SB07/manifest.md`
- Semantic invariants: `bundle://proof/SB07/semantic-invariants.md`
- Focused integration transcript: `bundle://proof/SB07/transcripts/focused-integration.txt`
- Source assertions: `bundle://proof/SB07/transcripts/source-assertions.txt`
- Forbidden hook scan: `bundle://proof/SB07/transcripts/forbidden-hook-scan.txt`
- Anti-stub audit: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`
- Failing-first source assertion: `bundle://proof/SB07/transcripts/failing-first-source-assertion.txt`
- Browser validation note: `bundle://proof/SB07/transcripts/browser-na.txt`

## Browser Validation Logging
- N/A: this subbundle has no browser-visible behavior.

## Progression Gate
- SB08 may proceed only after scheduler/workflow process paths are proven without driver side effects.
- Reopen SB07 if final source scans find direct driver hooks or mutation-capable verification jobs.

## Suggested Agent Prompt
- Implement SB07 by proving scheduler/workflow-origin launch through process-owned services and read-only verification job lifecycle without direct driver hooks. Store transcripts and scans under `proof/SB07/`.
