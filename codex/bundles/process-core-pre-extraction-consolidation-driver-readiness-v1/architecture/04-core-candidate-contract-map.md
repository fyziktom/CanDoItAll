# Test-Only Core Candidate Contract Map

## Scope

This map is documentation and test guidance only. It does not create a production Core project, public API, package, DI registration, or runtime adapter.

## Candidate Contract Families

| Candidate | Current owner | Allowed future Core shape | Denied dependencies |
| --- | --- | --- | --- |
| Route eligibility | `ProcessDispatchRouteEligibility` | Pure functions over run and step status values. | EF, claim leases, route handlers, route adapters, logging, AgentFramework execution. |
| Route stage order | `ProcessDispatchRoutePipeline` | Immutable route-stage descriptors and deterministic order checks. | Claim acquisition, transition execution, handler orchestration. |
| Route DTO snapshots | `ProcessRouteCandidate`, `ProcessRouteDispatchClaim`, route snapshot models | Immutable read models after dispatcher source payloads are fully removed. | `ProcessRunAutomationDispatchService` nested aliases, sidecar adapters, EF entities. |
| Finalizer intent records | `ProcessDispatchFinalizerInputs` | Intent records that describe workflow, recovery, direct-agent, and subprocess completion context. | Applying transitions, finalizer execution, route model adapter conversion. |
| Hydration pure assembly | `ProcessDispatchHydratedCandidateAssembler` and candidate factory records | Pure assembly over already-loaded snapshots. | EF queries, project-structure access mutation, manual recovery query, cooperation metadata lookup. |
| Pre-execution facts | `ProcessMissingUpstreamArtifactMaterialization` and database requirement decision records | Deterministic facts, fingerprints, and request builders. | Journal writes, rerun dispatch, transition writes, claim renewal. |
| Subprocess lifecycle rules | `ProcessSubprocessLifecycleRules` and `ProcessSubprocessArtifactSourceResolver` | Status mapping, transition request shaping, artifact source matching. | Child-run orchestration, projection persistence, save changes, gap journal writes. |
| Direct-agent execution input | `ProcessDispatchDirectAgentExecutionInput` | Route-owned input/output records if they remain side-effect free. | AgentFramework execution, provider repair, no-progress journaling, adapter conversion. |
| Artifact expectation snapshots | `ProcessArtifactExpectationSnapshot`, matcher/resolver/rule classes | Path, text, provider-native, lineage, and satisfaction rules over snapshots. | Storage writes, workspace filesystem reads, content readers, projection writes, validation orchestration. |
| Wrapper inventory output | `bundle://analysis/04-static-wrapper-inventory.md` | Candidate list for later extraction proposal. | Treating broad dispatcher compatibility methods as Core contracts. |

## Required Future Tests

Future Core proposal tests must prove all of the following before any production project is created:

1. Allowed candidates compile without references to EF, storage, workspace, AgentFramework execution, logging, route adapters, finalizer application, claim lifecycle, or transition services.
2. Denied dependencies remain in `CanDoItAll.Modules.Processes`.
3. Dispatcher compatibility aliases are not exposed as Core contracts.
4. Production driver APIs remain absent unless a separate driver bundle explicitly approves them.

## Decision

Docs/tests only. No production Core project is created by this bundle.
