# 08 Create, Clone, Snapshot, and Final Validation

## Status

- `Completed`

## Objective

- Complete empty-create, clone, snapshot, and IPFS transport flows and execute the final full-stack validation needed to close the feature honestly.

## Covered Inputs

- `RQ-012` empty database create completion
- `RQ-013` optional clone/snapshot branch flow
- `RQ-014` local and IPFS snapshot transport
- `RQ-019` full unit coverage
- `RQ-020` full integration coverage
- `RQ-022` final Playwright/browser proof
- `RQ-023` anti-fake closure enforcement
- Raw notes `N-10`, `N-15`, `N-16`, `N-17`, `N-18`

## Prerequisites

- `subbundles/04-migrations-and-legacy-upgrade-path` completed with proven SQLite and PostgreSQL bootstrap.
- `subbundles/05-storage-isolation-and-managed-files-serving` completed with file-isolation proof.
- `subbundles/06-runtime-reload-and-workbench-isolation` completed with cross-tab and stale-route proof.
- `subbundles/07-startup-modal-global-switcher-and-settings-ui` completed with reviewed UI proof.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`
- `C:\repositories\CanDoItAll/docker-compose.yml`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs`

## Deliverables

- Completed empty-create flows for both SQLite and PostgreSQL exposed through the backend and UI.
- Provider-agnostic snapshot package format and clone/import/export implementation.
- Local snapshot transport implementation.
- IPFS snapshot transport implementation with automated tests against a fake HTTP server and real-node proof when available.
- Final integration/component/browser regression coverage across switching, create, clone, storage continuity, and cross-tab reload.
- Final closure updates to `reviews/01-execution-report.md`, including honest blocked states if environment dependencies are missing.

## Dependency Impact

- This is the final closure phase that proves the main goal and the user's branch/versioning note.
- Weak proof here invalidates the entire bundle because clone/snapshot/IPFS behavior is explicitly part of the requested design direction.
- This phase is also responsible for preventing fake completion by enforcing the execution-report closure contract.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Finish any remaining empty-create backend/API/UI paths for SQLite and PostgreSQL if they were intentionally staged earlier.
2. Implement the provider-agnostic snapshot package format, including manifest data, per-table data export/import, and profile-scoped storage file inclusion.
3. Implement clone/new-from-clone flows that create a new target profile from a source profile or snapshot package.
4. Implement local snapshot transport and IPFS transport, including add/download/pin flows through the IPFS API client.
5. Add unit and integration tests for snapshot export/import, clone divergence, local transport, and fake-server IPFS transport behavior.
6. Add or extend Playwright flows to prove create, switch, clone, and cross-tab reload together from the user perspective.
7. Run the full required test matrix and update the execution report, browser analytics, and raw note closure sections honestly.
8. Perform the final QA/architect closure review; reopen any earlier critical subbundle if the final proof reveals a weak foundation.

## Scope Exceptions

- Real-node IPFS proof may close as `Blocked` only if a real node/API contract is genuinely unavailable; fake-server transport proof is still mandatory.
- Cross-machine secret portability is outside v1 scope; if snapshot portability across machines is evaluated, secrets may need explicit re-entry.

## Do Not Do

- Do not claim clone/snapshot is done if only DB rows were copied and profile-scoped storage was ignored.
- Do not claim IPFS support with only interface stubs and no automated transport proof.
- Do not claim PostgreSQL support if the final matrix never touched a real PostgreSQL instance.
- Do not leave the execution report with planned/pending placeholders and still mark the bundle complete.

## Acceptance Checklist

- A new empty SQLite database can be created, activated, and used.
- A new empty PostgreSQL database can be created, activated, and used.
- A clone/snapshot can create a new profile containing both source data and source storage files.
- After cloning, changes in the source and clone diverge independently.
- Local snapshot transport works.
- IPFS transport works against automated fake-server tests and, when available, against a real node.
- Full unit, integration, component, and Playwright proof is recorded honestly.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`
- PostgreSQL environment proof via `docker compose up -d postgres` or equivalent documented target, plus the PostgreSQL-backed integration/browser command subset.
- Fake-server IPFS transport test output and real-node proof when a node is available.
- Final reviewed screenshots for create/clone/switch/cross-tab flows.
- Final prepared/closure validator outputs and updated execution-report tables with no dishonest placeholder status.

## Browser Validation Logging

- Target routes: `/settings` for create/clone/snapshot flows, `/projects` or another seeded data page for data isolation proof, and a second page/tab for cross-tab behavior.
- Required viewport passes: `1600x1000` desktop first and `1100x900` follow-up if clone/snapshot forms or dialogs wrap.
- Required actions: create profile, activate it, seed data/files, clone or snapshot current profile, activate clone, verify isolated data and files, open a second tab, switch again, verify both tabs reload correctly.
- Required evidence paths: `evidence/db-switch-clone-flow-desktop.png`, `evidence/db-switch-cross-tab-desktop.png`, `evidence/db-switch-snapshot-ipfs-desktop.png`, `evidence/db-switch-final-responsive.png`.
- Screenshot review questions:
  - Is the clone/snapshot target clearly distinct from the source profile?
  - Do data and files both reflect the active profile correctly?
  - Does cross-tab behavior remain safe after clone/switch actions?

## Progression Gate

- Final closure only passes when the full test matrix is executed or honestly blocked and the execution report has no fake success markers.
- If final proof exposes weakness in subbundle 02–07, reopen that subbundle instead of closing here.

## Suggested Agent Prompt

```text
Implement subbundle 08 only.

Finish and prove the feature end to end:
- empty-create completion
- clone/snapshot package implementation
- local + IPFS transport
- final full test matrix
- honest execution-report closure

Do not hide blocked PostgreSQL/IPFS/browser dependencies.
If final proof exposes a weak foundation, reopen the earlier subbundle and document it.
```
