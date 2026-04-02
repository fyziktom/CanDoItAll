# Phase 02 Provider Services Routing And Batch Pipeline

## Status

- `Ready for implementation after Phase 01 gate`

## Objective

Build the runtime layer: registry, factory, catalog services, concrete drivers, unified access service, and batch transfer pipeline.

## Covered Inputs

- N002
- N003
- N004
- N007
- N008
- N010
- N011
- N012
- N014
- RQ-005
- RQ-006
- RQ-007
- RQ-008

## Prerequisites

- `subbundles/01-phase-01-models-interfaces-and-persistence-contracts` completed with stable contracts and migration plan.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/ResourceModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/architecture/01-target-solution.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/analysis/03-storage-touchpoint-scan.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/03-command-sequence.md

## Deliverables

- Driver registry, factory, catalog, routing, recommendation, access, and connection-test services.
- Concrete FileSystem, IPFS, and FTP provider implementations.
- Unified access route/service for preview/download/local-open capability checks.
- Manifest-driven transfer pipeline for folder migration and batch uploads.
- Nested workstream notes under `workstreams/` for provider/runtime slices.
- Nested workstream files listed below:
- `P2-WS01` - Registry, factory, catalog, and connection-test services (`workstreams/01-p2-ws01-registry-factory-catalog-and-connection-test-services.md`)
- `P2-WS02` - Filesystem driver and compatibility gateway (`workstreams/02-p2-ws02-filesystem-driver-and-compatibility-gateway.md`)
- `P2-WS03` - IPFS and FTP drivers (`workstreams/03-p2-ws03-ipfs-and-ftp-drivers.md`)
- `P2-WS04` - Unified storage access endpoint and capability-driven actions (`workstreams/04-p2-ws04-unified-storage-access-endpoint-and-capability-driven-actions.md`)
- `P2-WS05` - Batch transfer and migration pipeline (`workstreams/05-p2-ws05-batch-transfer-and-migration-pipeline.md`)

## Dependency Impact

- Phase 03 cannot build trustworthy provider contract tests or browser-proof scenarios without the Phase 02 runtime services.
- Phase 04 browser flows depend on access descriptors and capabilities from this phase.
- Weak proof here would make later UI screenshots misleading because the underlying provider actions could still fail.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Register and wire the storage runtime services in DI.
2. Refactor the current filesystem implementation behind the new driver contract.
3. Implement IPFS and FTP drivers with honest capability reporting.
4. Add the unified access route/service and gate local-open by capability.
5. Implement the batch transfer pipeline and refactor at least one bulk-copy path onto it.

## Scope Exceptions

- Do not claim full UI adoption here; Phase 04 owns module/page adoption.
- If no real FTP integration proof path is available, keep the FTP workstream blocked and document it.

## Do Not Do

- Do not keep remote-provider logic hidden behind fake filesystem relative paths.
- Do not expose unsupported actions as if they work.
- Do not mark FTP complete from compile-time or mocked-only proof.

## Acceptance Checklist

- Registry/factory resolve concrete drivers from catalog records.
- Unified access descriptors drive preview/download/local-open availability.
- At least one batch/migration path uses the shared pipeline.
- Filesystem behavior remains safe and compatible.

## Proof Required

- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- Targeted unit/integration tests from `plan/03-command-sequence.md`.
- At least one browser or HTTP smoke proof for the unified access route, logged in the execution report.

## Browser Validation Logging

- Target route: unified access route or a changed preview surface that consumes it.
- Viewport: `1900x1200` if a browser surface is used; direct HTTP proof is allowed only for non-UI service verification.
- Capture at least one before/after access proof for a non-filesystem-capable object if possible.

## Progression Gate

- Do not start Phase 04 browser-visible adoption until the access service and capability model are stable.
- Phase 03 must know whether FTP proof is real or blocked before it defines closure criteria.

## Suggested Agent Prompt

```text
Implement Phase 02 only.

Build the storage runtime services, concrete providers, unified access service, and batch transfer pipeline.
Preserve filesystem safety.
Do not fake FTP proof.
Do not bypass the new access/capability model.

Read this phase README, the nested workstream notes, the workbook inventories, and the execution checklist before changing code.
Update reviews/01-execution-report.md as you go.
Do not skip Playwright MCP proof when a browser-visible surface is touched.
```

