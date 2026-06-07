# Final Red-Team And Line-Count Review

## Decision

Ready for a narrow Process Core proposal next, with strict scope limits.

This is not approval to create a broad Core project or move orchestration. The first proposal must target pure read models and deterministic rules only, with failing architecture tests before any production move.

## Narrow First Candidates

| Candidate | Current evidence | Why eligible | Required guard |
| --- | --- | --- | --- |
| Route stage order and route eligibility descriptors | `ProcessDispatchRoutePipeline`, `ProcessDispatchRouteEligibility`, `bundle://proof/SB006/manifest.md`, `bundle://proof/SB027/manifest.md`, `bundle://proof/SB034/manifest.md` | Small, deterministic, source-payload-free after this bundle. | Core project must reject EF, workspace/storage, claims, AgentFramework, finalizer, and driver tokens. |
| Subprocess artifact source mapping and lifecycle status rules | `ProcessSubprocessArtifactSourceResolver`, `bundle://proof/SB018/manifest.md`, `bundle://proof/SB027/manifest.md`, `bundle://proof/SB034/manifest.md` | Pure mapping/status facts can be proposed without child-run orchestration. | Keep child-run observation, projection persistence, gap journals, and parent finalizer calls module-local. |
| Artifact expectation snapshots and pure matching rules | `ProcessArtifactExpectationSnapshot`, `ProcessArtifactExpectationMatcher`, `bundle://proof/SB024/manifest.md`, `bundle://proof/SB034/manifest.md` | Snapshot/matcher behavior is deterministic; projection writes and validation orchestration stay out. | Keep storage, workspace IO, recovery lineage persistence, provider-native artifact imports, and validation writes module-local. |

## Broad Core Extraction Blockers

| Blocker | Current evidence | Required next action |
| --- | --- | --- |
| EF and database context coupling remains in process dispatch | Static scan found `AppDbContext`, `DbContext`, `IDbContextFactory`, or `SaveChangesAsync` in 34 dispatch files. | Do not move EF-backed hydration, subprocess projection persistence, workflow coordination, or validation orchestration into Core. |
| Workspace, storage, and filesystem coupling remains broad | Static scan found workspace/storage/filesystem tokens in 95 dispatch files. | Extract only normalized value objects/rules; keep file reads/writes, storage placement, managed paths, and workspace boundary checks module-local. |
| AgentFramework execution is still the dominant runtime dependency | Static scan found AgentFramework/execution-run tokens in 134 dispatch files. | Keep execution, provider repair, retry, prompt assembly, proof capture, and browser/tool validation outside Core. |
| Claim and transition lifecycle is still side-effectful | Static scan found claim/transition tokens in 52 dispatch files. | Keep `TransitionStepWithClaimAsync`, lease checks, lost-claim handling, and route handler orchestration application-local. |
| Finalizer ownership remains application behavior | Static scan found finalizer/finalize tokens in 29 dispatch files. | Keep finalizer application, context recovery, and step-completion mutation in the process module. |
| Production helper-driver APIs remain intentionally absent | `bundle://proof/SB033/manifest.md` and `bundle://proof/SB034/manifest.md` prove no production driver API/registry/DI/runtime hook. | Future driver work must start as a contract proposal with no runtime dispatch or manager tool implementation. |

## Line-Count Review

| File | Lines | Red-team assessment |
| --- | ---: | --- |
| `ProcessRunAutomationDispatchService.ArtifactValidation.cs` | 2153 | Too large and side-effectful for Core; only small pure validation facts may be split later. |
| `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | 1433 | Must remain module-local because it owns finalizer application and mutation. |
| `ProcessRunAutomationDispatchService.ExecutionMetadata.cs` | 1272 | Too coupled to execution details and AgentFramework metadata for first Core cutline. |
| `ProcessArtifactProjectionFacetImplementations.cs` | 983 | Projection facets touch workspace/storage behavior; keep infrastructure edges local. |
| `ProcessRunAutomationDispatchService.Concurrency.cs` | 975 | Claim and competing-execution behavior remains application-local. |
| `ProcessRunAutomationDispatchService.ProjectPaths.cs` | 950 | Path/workspace behavior is not a Core candidate. |
| `ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | 934 | Recovery orchestration remains application-local. |
| `ProcessRunAutomationDispatchService.GovernedRules.cs` | 919 | Mine for pure facts later, but do not move wholesale. |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | 888 | Agent/tool validation stays out of Core. |
| `ProcessRunAutomationDispatchService.Grounding.cs` | 845 | Grounding behavior remains module/application-local. |

## Red-Team Negative Cases

- A future Core proposal that moves EF, filesystem, storage placement, claim lifecycle, AgentFramework execution, finalizer application, or runtime driver dispatch must fail architecture tests.
- A future proposal that creates a production helper-driver interface, registry, DI registration, manager command, or runtime selector in the same bundle as the first Core cutline is too broad.
- A future proposal that moves a large partial dispatcher file wholesale is too risky. It must move one small pure family at a time and preserve module-local adapters.

## Recommendation

Proceed next with a narrow Process Core proposal only for one pure family, starting with failing architecture tests and route/subprocess/artifact parity tests. Defer any production driver API until after the Core cutline proves dependency discipline.
