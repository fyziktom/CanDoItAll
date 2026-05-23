# Skip Cognitive Memory Integration Points

## Status

- `Completed`

## Objective

Gate optional Cognitive Memory integration points so disabled runtime usage produces explicit skips instead of recall, ingestion, consolidation, proposal scans, or project-scope failures.

## Success Criteria

- Agent context contributor skips before project-scope resolution when disabled.
- Workflow memory executors return deterministic skipped payloads before validating executor project settings.
- Scheduled automation returns not executed before downstream memory work.
- Enabled-mode failure behavior remains unchanged.

## Covered Inputs

- `N001`, `N002`, `N005`, `N006`
- Requirements: `R003`, `R004`, `R005`, `R006`

## Prerequisites

- SB01 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalServicesTests.cs`

## Deliverables

- Disabled guard in `CognitiveMemoryAgentContextContributor`.
- Disabled guard in recall/probe/learning proposal workflow executors.
- Disabled guard in `CognitiveMemoryScheduledAutomationRunner`.
- Tests proving disabled mode prevents downstream calls.

## Dependency Impact

- This subbundle closes the reported runtime failure.
- If any optional integration still calls memory while disabled, demos remain fragile and SB03 database proof cannot be trusted as user-ready closure.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add early disabled check in agent context contributor after settings load.
2. Add early disabled checks to Cognitive Memory workflow executors before executor settings validation.
3. Add early disabled check to scheduled automation before actor/take validation and downstream services.
4. Add unit tests for disabled context contributor and scheduled automation.
5. Preserve existing enabled-mode tests for missing project scope and unavailable memory.

## Scope Exceptions

- Direct Cognitive Memory management endpoints and pages are not removed or globally hidden.

## Do Not Do

- Do not catch and suppress arbitrary Cognitive Memory exceptions while enabled.
- Do not change MAF provider exception semantics.
- Do not unregister contributors/executors at startup.

## Acceptance Checklist

- [x] Disabled contributor returns `Skipped` with disabled reason and never calls recall.
- [x] Disabled workflow executors return skipped payloads and do not require project ids.
- [x] Disabled scheduled automation returns `Executed = false` and no downstream service calls.
- [x] Enabled missing-project-scope test still fails/skips according to existing policy.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- Command transcript for targeted runtime guard tests.
- Source assertion for all guarded integration points.
- Anti-stub audit showing tests use recording/throwing fakes to detect accidental downstream calls.
- Changed-file hashes.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure.

## Production Behavior Artifact Matrix

| Signal/state | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Disabled skip trace reason | Runtime guard helpers | Agent context traces and workflow/automation results | Emitted each disabled call | Pending guard tests. |
| Skipped workflow payload | Workflow executors | Workflow runtime/downstream nodes | Returned instead of memory payload | Pending source/test proof. |

## Browser Validation Logging

- N/A. This subbundle changes backend/runtime integration behavior.

## Progression Gate

- SB03 may start only when targeted tests prove disabled guards avoid downstream memory calls and enabled behavior remains strict.

## Suggested Agent Prompt

```text
Implement SB02 only. Use the SB01 setting to gate agent context, workflow memory executors, and scheduled automation. Disabled means explicit skip/no-op before downstream memory calls; enabled behavior remains strict. Add tests that would fail if recall/ingestion/consolidation/proposal scanning is accidentally called.
```
