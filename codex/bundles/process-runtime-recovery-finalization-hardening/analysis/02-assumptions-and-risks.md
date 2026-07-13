# Assumptions And Risks

## Assumptions

- The existing process project split is intentional and should be improved, not replaced by a new runtime framework.
- Existing process templates and AgentFramework integration must continue to run while contracts are introduced incrementally.
- Runtime can add generic contracts for artifact lineage, finalization, handoff, and retry classification without knowing software-development concepts.
- Domain-specific process drivers can add .NET, browser, project-structure, and delivery policy on top of generic contracts.
- Existing tests around process runtime, dispatch application service, process adapter, and manager loop are the first characterization surface.

## Critical Path Risks

- SB02 is a critical path risk because finalization and retry routing cannot be reliable if connected input artifacts are still represented only as slot ids.
- SB03 is a critical path risk because agents and finalizers need fresh step-contract retrieval after context compression.
- SB04 is a critical path risk because process advancement must be blocked until finalization proves required inputs were consumed and outputs were produced.
- SB05 is a critical path risk because retries can remain harmful if missing upstream artifacts, missing tools, denied access, and agent non-compliance are not separated.
- SB06 is a critical path risk because moving policies without real driver seams would only reshuffle partial classes.

## Validation Risks

- A test that asserts only status `Ready`, `Blocked`, or `Completed` can pass while still missing artifact lineage or manager confirmation.
- A test that seeds `AvailableArtifactSlots` directly can bypass the production artifact producer and falsely prove downstream readiness.
- A test that uses one direct previous step can miss the bug class where the required artifact comes from an earlier non-direct connected step.
- A test that uses a single software-development template can accidentally bake .NET assumptions into generic runtime contracts.
- A finalizer test that only checks a required tool receipt can miss missing upstream input readback.
- CodeAnalytics cannot prove semantic behavior alone; exact source and test proof are still required.

## Reopen Triggers

- Reopen SB02 if any downstream phase needs a concrete artifact reference and only has an `ArtifactSlotId`.
- Reopen SB03 if an agent or finalizer cannot retrieve the current step contract, expected outputs, required artifacts, or manager handoff requirements from a tool-backed source.
- Reopen SB04 if a completed step can advance without evidence that required connected inputs were read or required produced outputs were written.
- Reopen SB05 if any missing upstream artifact, denied tool, missing access grant, or missing manager decision is classified as same-step automatic retry.
- Reopen SB06 if implementation adds another partial file to `ProcessRuntimeEngine` or `AgentFrameworkProcessExecutionAdapter` without a temporary removal plan.
- Reopen SB07 if downstream process context contains full arbitrary product file dumps instead of bounded artifact packages and retrieval handles.
- Reopen SB08 if proof uses manually seeded runtime state instead of production launch, dispatch, adapter, artifact, and manager paths.
