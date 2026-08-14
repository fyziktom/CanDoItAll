# CP0 — baseline and decision review

Status: Pass

## Review inputs

- SB00 proof manifest and handoff;
- current project/reference graph;
- exact canonical identity/provider/API conventions;
- focused baseline test evidence;
- no-production-diff proof.

## Decisions

| Area | Decision | Evidence |
|---|---|---|
| organization scope | Profile-local; no organization column | `IDatabaseRuntimeState`; `AgentFrameworkWorkspaceFactory.GetOrganizationScope` shows agent-only profile-derived scope |
| authenticated subject | No per-user ownership; authorized profile-local resources | `ApiEndpointRouteBuilderExtensions`; `MemoryProvidersApi.ResolveRequesterId` |
| provider resolver | Extract `IProviderRuntimeProfileSource` contract to Providers; keep canonical snapshot implementation | `Core/Contracts/Contracts.cs`; `ProviderRuntimeProfileSnapshotService.cs` |
| API auth/error conventions | Conditional `/api` authorization + explicit read/write/execute policies + `ApiEndpointResults` | `ApiEndpointRouteBuilderExtensions.cs`; `ApiAuthorizationPolicies.cs` |
| transaction helper | `SerializableMutationScope` plus explicit affected-row CAS | `SerializableMutationScope.cs`; `AppDbContext.SaveChangesAsync` conventions |
| cancellation/activity helper | New narrow LLM Chat operation registry/scope | Existing helpers are agent/process/streaming-owned |
| profile-switch drain/commit coordination | Existing restart-only activation is sufficient; retain/test generation fence | `DatabaseSwitchCoordinator`; `DatabaseRuntimeState` |
| thinking-effort capability/request seam | Reuse typed Models policy; add optional `LlmModelSettings` override and safe provider options | CodeAnalytics `snap-20260814152917-c5b941c8`; `AgentThinkingEffortPolicy.cs` |

## Verdict

- [x] Pass — unlock SB01
- [ ] Reopen SB00
- [ ] Stop bundle
