# 05 Storage Isolation and Managed Files Serving

## Status

- `Completed`

## Objective

- Make workspace storage profile-scoped and replace fixed startup-time managed-file serving with runtime-aware serving that follows the active profile safely.

## Covered Inputs

- `RQ-008` profile-scoped storage roots
- `RQ-009` runtime-aware managed-file serving
- `RQ-013` clone/versioning storage completeness foundations
- Raw notes `N-11`, `N-16`

## Prerequisites

- `subbundles/02-control-plane-and-profile-catalog` completed with storage-root metadata in profile descriptors.
- `subbundles/03-dynamic-runtime-db-and-bootstrap` completed with active-profile resolution.
- `subbundles/04-migrations-and-legacy-upgrade-path` should be complete or stable enough that seeded files can be associated with migrated profiles.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureRuntimeLauncher.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs`

## Deliverables

- Profile-aware workspace path resolution for managed files, exports, and evidence.
- Runtime-aware managed-file endpoint or middleware that resolves the active profile per request and prevents path traversal.
- Updated `IFileStore` / `IManagedArtifactStore` behavior that writes into the active profile's workspace root.
- Updated local-file opener and runtime launcher logic that validates paths against the active profile root.
- Tests proving different profiles serve different file roots and that managed-file URLs still work after switching.

## Dependency Impact

- Clone/snapshot completeness in subbundle 08 depends on storage isolation implemented here.
- Runtime switch UX in subbundle 06 and 07 is untrustworthy if managed files still resolve to the wrong profile after switching.
- Host integrations such as local open and runtime launch will remain unsafe until they are root-aware.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Refactor `IWorkspacePathResolver` and related storage services so profile-scoped roots come from the active profile instead of a single global content-root path.
2. Replace the fixed `/managed-files` static-file binding with a request-time handler/endpoint that resolves the active profile root and rejects path traversal.
3. Update `LocalFileStore` and `ManagedArtifactStore` to write into profile-scoped managed roots.
4. Update local file open and runtime launch flows so their trusted-root checks use the active profile storage root.
5. Add integration tests that seed distinct files in two profiles, switch profiles, and verify the served/downloaded file changes correctly.
6. Add traversal/invalid-path tests so the new endpoint cannot escape the profile root.

## Scope Exceptions

- This subbundle does **not** yet implement clone/snapshot orchestration, but it must provide the storage behavior clone/snapshot later depends on.
- This subbundle does **not** yet expose UI affordances for storage selection; profile metadata already carries those values.

## Do Not Do

- Do not keep `UseStaticFiles(new PhysicalFileProvider(...))` as the active `/managed-files` implementation and still mark this phase complete.
- Do not ignore host-side path validation when changing the workspace-root resolver.
- Do not prove only DB switching while leaving storage rooted to the wrong profile.

## Acceptance Checklist

- Different profiles resolve different workspace roots for managed files, exports, and evidence.
- Managed-file URLs continue to work correctly after a runtime profile switch.
- Path traversal requests are rejected.
- Local open/runtime launch logic still trusts only files inside the active profile root.
- The execution report includes at least one file-isolation proof case.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Storage|FullyQualifiedName~PathResolver|FullyQualifiedName~ManagedFiles"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Storage|FullyQualifiedName~ManagedFiles|FullyQualifiedName~Traversal"`
- Browser or HTTP proof of a managed-file URL before and after a profile switch, logged in the execution report.
- Capture the profile-specific file paths and the screenshot or HTTP assertion that proves the switch changed the served file.

## Browser Validation Logging

- Target route: a page that renders or links to a managed file, or the direct `/managed-files/...` URL for a seeded file.
- Required viewport pass: `1600x1000` if a product page is used; `N/A` if direct HTTP verification is sufficient and no UI changed.
- Required actions: seed distinct files in two profiles, switch profile, re-request the file, and verify the content/path changed.
- Required evidence paths: `evidence/db-switch-managed-file-before.png`, `evidence/db-switch-managed-file-after.png` when browser-visible UI is involved.
- Screenshot review question: Does the rendered/downloaded file clearly belong to the active profile and only that profile?

## Progression Gate

- Managed-file serving must be runtime-aware and profile-scoped before subbundle 06 and 08 continue.
- The execution report must show at least one positive file-isolation proof and one traversal rejection proof.

## Suggested Agent Prompt

```text
Implement subbundle 05 only.

Make storage profile-scoped:
- active-profile-aware workspace roots
- runtime managed-files endpoint
- updated file stores
- updated local open/runtime launch safety checks
- tests for file isolation and path traversal

Do not implement clone orchestration yet.
Record file-isolation proof honestly.
```
