# BR07 — Architecture guards and focused gates

## Objective

Convert the corrected ownership into durable automated constraints and run the final non-container acceptance lane.

## Required guards

Add or strengthen tests that fail when:

1. ProviderManagement references Workspace.
2. provider-specific AgentFramework source references Workspace.
3. Workspace owns provider/shared-provider entities, services, runtime, transfer, or DI registration.
4. Web shared-provider endpoints import Workspace provider services.
5. Agent provider UI injects Workspace provider services/types.
6. Workbench uses the legacy Workspace provider execution stack.
7. a production direct inference adapter/registry path is reintroduced.
8. provider/shared-provider EF CLR types are configured from Workspace assembly.
9. physical table names change unexpectedly.
10. ProviderManagement DI is registered zero times or more than once in the production host.
11. user-facing source reintroduces “Workspace-backed/owned provider” terminology.

Prefer compile-time/project-graph and source-boundary tests over brittle broad text assertions. Keep the included Python guard as an independent fast gate.

## Final focused behavior lane

Run focused non-container tests for all items in `FINAL-ACCEPTANCE.md`, including:

- personal provider lifecycle and secret mutation
- provider deletion policy
- publication/catalog redaction
- source sync/import reconciliation/deletion policy
- personal/shared/hybrid materialization
- runtime revision snapshots and fail-closed behavior
- relay authorization/rate limiting/audit/recovery
- image target routing
- Workbench MAF-backed execution
- UI/API/DI/transfer compatibility

## Commands

1. Run affected builds using outputs restored once.
2. Run focused test projects/filters.
3. Run architecture tests.
4. Run:

   ```bash
   python codex/bundles/shared-providers-boundary-recovery/scripts/check_provider_boundary.py \
     --repo . \
     --mode final \
     --output artifacts/provider-boundary-final.json
   ```

5. Run the locally supported EF pending-model check once.
6. Run a full non-container unit suite only after focused gates pass and only once.
7. Run `git diff --check`.

Do not run Docker, Podman, browser E2E suites, or original SB07 lifecycle commands.

## Acceptance

Every mandatory item in `FINAL-ACCEPTANCE.md` that does not explicitly require deferred Docker infrastructure is green. Any deferred item names the exact original SB07 lane that owns it.

## Commit

`BR07: enforce provider boundary and final gates`
