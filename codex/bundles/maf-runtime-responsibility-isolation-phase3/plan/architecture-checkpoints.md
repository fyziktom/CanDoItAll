# Architecture Checkpoints

## Checkpoint After SB01

- Responsibility inventory covers every large/hotspot type named in the bundle.
- CodeAnalytics snapshot id and exact symbol evidence are recorded.
- Characterization tests to add before movement are listed by behavior.
- Decision: SB02/SB04/SB05 may start only if the inventory maps every moved member to a target owner.

## Checkpoint After SB02

- `MafAgentRuntime` has fewer responsibilities and delegates turn orchestration.
- No new runtime partial or nested architecture boundary exists.
- Direct unit tests instantiate `MafRuntimeTurnCoordinator`.
- Decision: SB03 may start only if runtime facade proof passes.

## Checkpoint After SB03

- Streaming/finalizer/session/approval drivers have direct positive and negative tests.
- Source assertions show moved methods no longer live in `MafAgentRuntime`.
- Performance/timing notes compare focused tests or runtime stage timings before/after.
- Decision: SB07 may not start until these drivers are independently testable.

## Checkpoint After SB04

- `MafRuntimeAgentFactory` has a smaller role.
- Script policy, handoff, finalizer tool, and instrumentation behavior have focused owners.
- Service-locator use is limited to composition or explicitly justified provider SDK creation.
- Decision: SB05 can proceed only if capability composer dependencies are explicit.

## Checkpoint After SB05

- No final `partial class RuntimeCapabilityComposer`.
- Capability access planner, descriptor catalog, and attachment orchestrator have direct tests.
- Extension seam test proves a fake capability provider can be added without editing old monoliths.
- Decision: SB06 may start only if workspace tool registration can use the new seam.

## Checkpoint After SB06

- Workspace tool families own cohesive behavior.
- Shared access policy/path service has direct denial tests.
- Host-visible command proof is captured for moved command/script tools.
- Decision: SB07 may start only after security/policy regressions are ruled out.

## Checkpoint After SB07

- Project references are documented and acyclic.
- DI registration resolves extracted collaborators.
- `IServiceProvider` usage in core behavior is eliminated or explicitly justified with a narrow factory reason.
- Decision: SB08 may start only if dependency direction and DI proof pass.

## Final Checkpoint SB08

- Final CodeAnalytics snapshot shows reduced hotspot ownership.
- Architecture gate is `Pass` or `Pass with follow-up`; blockers cannot be hidden as residual risk.
- Raw request closure is explicit.
- Follow-up bundle is created only for intentionally deferred hotspots such as deeper `McpCapabilityBuilder` split.
