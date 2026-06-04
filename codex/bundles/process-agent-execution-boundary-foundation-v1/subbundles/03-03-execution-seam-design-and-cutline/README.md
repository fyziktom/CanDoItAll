# SB03: Execution seam design and cutline

## Status

Prepared; not started.

## Objective

Design the process automation execution client/facade and identify exact source movement boundaries before implementation.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-artifacts.md`
- `inputs/03-large-screen-only-proof-policy.md`

## Prerequisites

Previous subbundle SB02 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/proof/SB12/source-assertions/next-phase-cutline.md`

## Deliverables / Scope

- Subbundle-specific source changes or proof artifacts.
- Updated tests/scans where applicable.
- Proof manifest and semantic invariants.
- Execution report entry.
- Large-screen-only UI proof decision.

## Dependency Impact

- Owned requirements: RQ-004, RQ-011, RQ-013
- Downstream subbundles must not start until this closure gate passes.
- If this subbundle is a gate/checkpoint, downstream proof is untrustworthy until the gate passes.

## Validation Depth

- Source scan proof.
- Targeted unit/integration proof when production code changes.
- Full build at gate or final phases.
- Browser validation: `N/A` unless UI changed; if UI changed, large-screen PC only.

## Implementation Steps

1. Design `IProcessAutomationExecutionClient` or equivalent facade.
2. Specify which current AgentFramework DTOs may temporarily pass through.
3. Specify which calls will move in SB06 and which remain out of scope.
4. Update architecture docs/proof notes with the staging model.
5. Do not implement the full DTO mapping yet.

## Scope Exceptions

- Full Process Core extraction is out of scope.
- Driver packs are out of scope.
- Small/medium/mobile proof is out of scope.

## Do Not Do

- Do not reintroduce MAF product-tool dependencies.
- Do not rename process tools.
- Do not weaken access policy.
- Do not move EF entities.
- Do not create driver packs.
- Do not run small, medium, or mobile UI validation.

## Acceptance Checklist

- [ ] Objective completed.
- [ ] Source assertions recorded.
- [ ] Targeted tests pass.
- [ ] Previous provider seam preserved.
- [ ] No forbidden product dependency introduced.
- [ ] No mobile/small/medium screenshots produced.
- [ ] Execution report updated.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/source-assertions/*.txt`
- `proof/SB03/transcripts/*.txt`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes rendered UI. If UI is changed, validate only in a large-screen PC viewport and record route, viewport, assertions, and screenshot. Do not test small/medium/mobile.

## Progression Gate

- Closure checklist complete.
- No reopen trigger active.
- Gate subbundles SB04, SB07, and SB10 require explicit refactor checkpoint sign-off.

## Suggested Agent Prompt

Implement SB03 from `process-agent-execution-boundary-foundation-v1`. Follow the exact source references, do not broaden scope, and do not proceed to the next subbundle until the progression gate passes.
