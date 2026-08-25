# Final acceptance

Every mandatory item must be true before the original SB07 work resumes.

## A. Compile-time ownership

- [ ] Project `CanDoItAll.Modules.AgentFramework.ProviderManagement` exists and is included in the canonical solution.
- [ ] Its `.csproj` has no Workspace project reference.
- [ ] Its source contains no `CanDoItAll.Modules.Workspace` reference.
- [ ] Provider-specific source under the AgentFramework Razor module contains no Workspace reference.
- [ ] `Modules.Workspace/SharedProviders` does not exist.
- [ ] `Modules.Workspace/Providers` does not contain provider ownership/runtime code.
- [ ] Workspace DI registers no provider/shared-provider adapter, registry, runtime gateway, control-plane service, or hosted worker.
- [ ] Web shared-provider APIs contain no Workspace provider import.
- [ ] Workbench contains no use of the legacy Workspace `ProviderExecutionService` request/response stack.

## B. Canonical data/application ownership

- [ ] Persisted provider-profile CLR entity/configuration belongs to ProviderManagement.
- [ ] All shared-provider persisted entities/configurations belong to ProviderManagement.
- [ ] Provider administration, secret mutation, health/pricing/manifest orchestration, and database transfer belong to ProviderManagement.
- [ ] Runtime profile materialization, revision snapshots, and commit observation have no Workspace dependency.
- [ ] Names such as `WorkspaceBackedAgentProviderProfileRegistry` and `WorkspaceAgentProviderProfileMapper` are removed.

## C. One runtime path

- [ ] Workbench prompt execution uses a neutral MAF-backed port.
- [ ] Shared-provider relay uses the same MAF-backed runtime path.
- [ ] No production direct-inference implementation remains for the old `IProviderAdapter`/`ProviderRegistry`/`ProviderExecutionService` stack.
- [ ] OpenAI, Ollama, ComfyUI, scenario, mock, and future providers resolve through the AgentFramework provider-driver/runtime composition.
- [ ] Health/model discovery helpers cannot send inference requests.

## D. UI and API

- [ ] `/agents?tab=providers` is authoritative and fully functional.
- [ ] Agent provider UI injects ProviderManagement ports, not `WorkspaceService`.
- [ ] Workspace provider settings route is either absent or a compatibility redirect without editor state.
- [ ] User-facing “Workspace-owned/workspace-backed provider” text is removed.
- [ ] Existing shared-provider API routes and wire DTOs remain compatible.
- [ ] Public catalog output remains secret-free.

## E. Persistence

- [ ] Existing physical provider/shared-provider table names are unchanged.
- [ ] Existing migrations remain in history.
- [ ] No migration drops, renames, recreates, or copies an existing provider/shared-provider table.
- [ ] Existing IDs, FKs, indexes, max lengths, revisions, and delete behaviors are retained.
- [ ] EF reports no pending model changes, or the only new migration is manually verified to have empty `Up` and `Down` methods.
- [ ] ProviderManagement assembly marker is registered in module model discovery.

## F. Behavior characterization

Focused tests cover at minimum:

- [ ] personal provider create/update/delete and secret replacement
- [ ] deletion policy when a provider is referenced
- [ ] publication eligibility and catalog redaction
- [ ] source discovery and deterministic import reconciliation
- [ ] imported provider deletion/disable behavior
- [ ] personal/shared/hybrid runtime materialization
- [ ] effective revision snapshot stability during one execution
- [ ] fail-closed missing secret/source/capability behavior
- [ ] relay authentication/authorization
- [ ] rate limiting and invocation audit/recovery
- [ ] image provider target resolution
- [ ] Workbench rewrite execution through the new port

## G. Automated guard

Run from repository root:

```bash
python codex/bundles/shared-providers-boundary-recovery/scripts/check_provider_boundary.py \
  --repo . \
  --mode final \
  --output artifacts/provider-boundary-final.json
```

It must exit zero.

## H. Build and test lane

Use the canonical solution/project paths discovered in BR00. Required minimum:

1. restore once
2. build ProviderManagement
3. build AgentFramework module
4. build Workspace
5. build Workbench
6. build Web/Composition host
7. run focused provider/shared-provider unit and integration tests without Docker
8. run architecture guards
9. run EF pending-model check where locally supported
10. `git diff --check`

A full non-container unit test suite is recommended in BR07 when the focused gates are green. Docker lifecycle validation remains deferred to the separately authorized continuation of original SB07.
