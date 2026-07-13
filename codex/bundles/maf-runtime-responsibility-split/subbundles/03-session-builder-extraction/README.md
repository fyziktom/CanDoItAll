# Session Builder Extraction

## Status

- `Completed`

## Objective

- Move session creation, restoration, prompt input, streaming snapshotting, run options, structured response format, and provider-history decisions into a dedicated session builder/collaborator.

## Covered Inputs

- N008
- Requirements R06, R09, R10

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- Session characterization tests are green before extraction.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRecoveryIntegrationTests.cs`

## Deliverables

- Internal session builder class with explicit input/options record if needed.
- `MafAgentRuntime` delegates session-related behavior to the builder.
- Tests cover restore/create decisions, approval transcript replay, request-scoped attachment removal, provider conversation-id restoration, structured response format, and framework-managed history decisions.

## Dependency Impact

- SB06 finalizer recovery depends on session serialization behavior.
- SB07 cannot safely slim runtime until session behavior is delegated and tested.
- SB08 UI proof depends on agent chat session behavior remaining stable.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add or update tests for current session behavior.
2. Extract session builder without changing call order.
3. Move streaming snapshot helpers only if they are session-owned and tests cover opaque tool-call snapshots.
4. Keep request-scoped attachment stripping compatible with existing attachment tests.
5. Replace runtime partial methods with builder calls.
6. Remove unused partial code after tests pass.

## Scope Exceptions

- Do not redesign chat history persistence or provider-managed conversation semantics.

## Do Not Do

- Do not silently ignore invalid serialized session state.
- Do not add provider-specific fallbacks hidden inside the builder.
- Do not move finalizer validation into the session builder.

## Acceptance Checklist

- Session builder has one cohesive responsibility.
- `MafAgentRuntime.Session.cs` is removed or reduced to a thin adapter only if needed temporarily.
- Existing attachment and recovery tests pass.
- New tests cover session restoration and structured response format decisions directly.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- Failing-first or characterization transcript for key session behavior.
- Passing unit and integration test transcripts.
- Source assertions showing runtime delegates to the session builder.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- If this subbundle introduces a production signal, state, record, or event, add a Production Behavior Artifact Matrix to both proof artifacts.

## Browser Validation Logging

- Deferred to SB08. Record any UI-visible session behavior risk in `reviews/01-execution-report.md` so SB08 can target it.

## Progression Gate

- SB06 and SB07 may start only after session builder tests and integration recovery tests pass.

## Suggested Agent Prompt

```text
Implement SB03 only. Extract session behavior into a focused builder, preserve provider/session compatibility, prove attachment and recovery invariants, capture proof under proof/SB03, and stop if finalizer or provider behavior starts leaking into the session builder.
```
