# Current State

## Bundle State

- `candoitall-canonical-architecture-review-bundle-v2` is stale against the current validator contract and cannot be honestly closed as-is.
- The validator requires a normalized root overlay plus subbundle contracts with explicit readiness and closure sections.

## Canonical Findings Reopened By The New Review

- Critical: node-local party relations are still dual-written into workbench metadata and CRM/HR assignments.
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.PartyIntegration.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- High: structure lifecycle flows do not reconcile node-scoped assignments.
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- Medium: the node-scope bridge still resolves by raw string `NodeKey`.
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodeScopeBridge.cs`
- Medium: current automated coverage does not protect delete and subtree-transfer assignment coherence.
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectPartyAssignmentIntegrationTests.cs`
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Live Repo Observations That Shape The Fix

- The project-facing bridge already owns node-scope validation, so it is the correct place for canonical node-assignment lifecycle operations.
- `ProjectStructurePage` currently loads participant, meeting, and work-item editor state from metadata instead of the validated assignment bridge.
- Workbench can safely depend on `IProjectPartyIntegrationBridge` without reaching into CRM/HR storage records directly.
- Existing component and Playwright tests already cover the editor surface, which allows a targeted repair instead of a new broad harness.
