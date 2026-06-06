# Core Readiness Decision Matrix

| Area | Current state after this bundle | Readiness | Next action |
| --- | --- | --- | --- |
| Route stage order | Explicit route pipeline with source-scan and focused integration proof. | High | Keep module-local for one more bundle; later extract only pure route-order contracts. |
| Route kind classification | Route planner remains pure enough for future contract work. | High | Prepare a narrow route-decision contract after adapter source payloads are removed. |
| Route service ownership | Route service file no longer references dispatcher adapters or dispatcher service dependency. | Medium-high | Move adapter source payloads out of route models before any Core extraction. |
| Candidate hydration | Hydration exposes route candidate reload but still owns EF, workspace, recovery, binding, and access mutation. | Low | Keep application-local; split into explicit query/binding/recovery collaborators before Core. |
| Pre-execution materialization | Database requirement and upstream materialization consume route candidate facts. | Medium-high | Keep application-local until materialization journal/request side effects are isolated. |
| Start transition | Transition request shaping uses module-local planner and route claim overload. | Medium | Extract only pure request shaping later; keep claim checks application-local. |
| Subprocess runtime | Route overload exists, but subprocess orchestration and projection persistence stay application-local. | Medium | Split projection persistence from orchestration in a later isolation bundle. |
| Finalizer/failure closure | Finalizer route overloads and failure closure service now hide dispatcher internals from route services. | Medium | Remove remaining dispatcher finalizer aliases before Core. |
| Static rule families | Existing rule classes remain preferred; this bundle avoided broad static rewrites beyond route boundary. | Medium | Burn down remaining pure wrappers only with focused parity tests. |
| Agent execution | Still AgentFramework/infrastructure integration. | Low | Keep out of Core. |
| Claim lifecycle | Still EF/lease/heartbeat heavy. | Low | Keep out of Core. |
| Artifact projection and storage | Still storage/file/workspace heavy. | Low | Keep out of Core; isolate projection DTOs separately. |
| Driver readiness | Documentation-only; no production driver APIs. | Not a production API | Define helper-driver contracts only after Core candidate contracts stabilize. |

## Final Decision

Do not create `CanDoItAll.Processes.Core` yet. The next bundle should remove dispatcher source payloads from route models, split hydration side-effect collaborators, and isolate finalizer/projection DTOs. Only after those cuts pass build, focused tests, source scans, and red-team proof should a Process Core project be reconsidered.
