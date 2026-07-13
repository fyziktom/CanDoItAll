# Runtime Orchestration Slimming

## Status

- `Completed`

## Objective

- Make `MafAgentRuntime` a runtime orchestrator that delegates helper, builder, and finalizer responsibilities to focused collaborators.

## Covered Inputs

- N001, N002, N006
- Requirements R02, R09, R10

## Prerequisites

- SB01 through SB06 closure gates passed.
- All extracted collaborators have proof manifests.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafContextManifestBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`

## Deliverables

- Runtime orchestration code delegates to extracted collaborators.
- Obsolete partial files are removed or reduced to justified adapters.
- Static scans prove requested helpers/builders/finalizer logic no longer lives in `MafAgentRuntime`.
- File-size thresholds from SB01 are met or explicitly blocked with evidence.

## Dependency Impact

- SB08 final proof depends on runtime slimming being real, not cosmetic.

## Validation Depth

- `Critical foundation`
- `Runtime hardening checkpoint`

## Implementation Steps

1. Wire extracted collaborators into runtime construction.
2. Remove dead private static methods from `MafAgentRuntime`.
3. Collapse or delete partial files that are now empty or only wrappers.
4. Run static scans for old method names and catch-all helper files.
5. Run MAF build and focused runtime tests.
6. Update execution report with line-count deltas.

## Scope Exceptions

- Do not refactor capability partials beyond call-site cleanup required for this bundle.
- Do not split `MafAgentRuntime.AgentFactory.cs` unless SB01 marked it necessary.

## Do Not Do

- Do not move code into a new file over the max collaborator threshold without another split.
- Do not leave duplicate old and new paths.
- Do not change public contracts for convenience.

## Acceptance Checklist

- `MafAgentRuntime.cs` meets the SB01 threshold or the subbundle is blocked.
- Static scan shows requested helper/builder/finalizer methods moved to named owners.
- No new catch-all helper file exists.
- MAF build and focused tests pass.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- Before/after line-count transcript.
- Static scan transcript for residual method names and helper dumping grounds.
- MAF project build transcript.
- Focused unit/integration test transcripts.
- Source assertions proving delegation.
- Changed-file hashes.
- Anti-stub audit.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Production Behavior Artifact Matrix if new production state, event, record, or runtime signal is introduced.

## Browser Validation Logging

- Deferred to SB08. This subbundle must list UI-visible risks for SB08 if runtime diagnostics or response state changed.

## Progression Gate

- SB08 may start only after size/static scans, build, and focused runtime tests pass.

## Suggested Agent Prompt

```text
Implement SB07 only. Wire extracted collaborators, remove dead runtime helper code, prove MafAgentRuntime is now an orchestrator, capture proof under proof/SB07, and stop if the code merely moves into another catch-all file.
```
