# Normalized Requirements

| ID | Requirement | Validation |
| --- | --- | --- |
| R01 | Preserve the raw user scope: generic MAF runtime refactor only, no Financial Strategist or document-domain feature work. | Traceability maps every requirement to MAF runtime files only. |
| R02 | Build and maintain a current-state responsibility inventory before implementation. | SB01 inventory table, CodeAnalytics snapshot, and source scans captured. |
| R03 | Make `MafAgentRuntime` a thin `IAgentRuntime` adapter. | Source assertion shows runtime delegates turn execution, approval continuation, runtime build, and diagnostics to injected/extracted collaborators. |
| R04 | Extract turn orchestration from `MafAgentRuntime`. | Direct unit tests instantiate the coordinator without `MafAgentRuntime`; integration smoke still passes through `IAgentRuntime`. |
| R05 | Extract provider streaming, finalizer repair, session persistence, and approval continuation drivers. | Direct unit tests cover success, provider failure, missing required finalizer, serialization skip, serialization timeout, and approval rehydration negative cases. |
| R06 | Decompose `MafRuntimeAgentFactory` into construction, handoff, instrumentation, finalizer tool, and script-policy owners. | New owners have focused tests; factory no longer owns script policy and handoff build internals directly. |
| R07 | Decompose `RuntimeCapabilityComposer` into non-partial owners for access planning, descriptor catalog, attachment orchestration, and context assembly. | No final `partial class RuntimeCapabilityComposer`; tests instantiate extracted owners directly. |
| R08 | Split `WorkspaceRuntimePlugin` into cohesive workspace tool families and shared access-policy/path services. | Tool family tests prove metadata, access policy, execution, error behavior, and registration. |
| R09 | Keep dependency direction valid and avoid new project cycles. | `.csproj` before/after table and CodeAnalytics dependency/cycle proof when references change. |
| R10 | Improve testability with explicit seams and fakes. | Each critical extraction has isolated unit tests, at least one negative test, and no live provider/network dependency. |
| R11 | Add architecture guards that prevent regression. | Tests fail on forbidden partials, nested runtime-owned collaborators, broad helper/manager names, source ownership regressions, and full-runtime-only tests for extracted behavior. |
| R12 | Capture performance-oriented evidence, not guesses. | Baseline and after timings for runtime construction/capability composition/focused test slice are recorded; any regression has an explanation or follow-up. |
| R13 | Maintain behavior compatibility through focused integration smoke. | MAF handoff smoke and runtime tool provider composition slices pass after relevant subbundles. |
