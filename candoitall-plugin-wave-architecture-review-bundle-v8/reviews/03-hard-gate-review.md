## Hard-gate review

- `HG-01` — **FAIL** — ProjectObjectRecord mapping no longer persists binding/media/artifact columns, foreign-owner IDs are not writable metadata, and no direct writes to node binding fields remain outside the binding boundary.
- `HG-02` — **FAIL** — Editable nodes use one hierarchy owner only, and no editable-node Contains/BelongsTo link rows are persisted.
- `HG-03` — **FAIL** — The node-kind registry/capability service resolves assignment roles and canonical-node scope. Hardcoded role/type switches are gone.
- `HG-04` — **FAIL** — Provider/resource editor and resolution flows are driven by connector manifests and plugin keys. Adding a synthetic plugin does not require enum expansion or switch-page edits.
- `HG-05` — **FAIL** — Write-side connector actions commit intent durably and execute through a worker/outbox/idempotent operation boundary instead of inline side effects + compensation.

### Automated gate script current run

```text
=== Phase8 plugin-gate check ===
Repo: /mnt/data/repo_phase9/CanDoItAll-canonical-model-refactor

Hard-gate failures:
- HG-01 FAIL: ProjectObjectRecord still maps binding columns: Route, ExternalArtifactKind, MediaRelativePath, MediaContentType, MediaOriginalFileName, StorageObjectReferenceJson
- HG-01 FAIL: ProjectWorkbenchLifecycleService still mutates binding fields directly: ExternalArtifactKind, Route
- HG-01 FAIL: ProjectWorkbenchCommandService still mutates binding fields directly: ExternalArtifactId, ExternalArtifactKind, Route
- HG-01 FAIL: ProjectWorkbenchCrossModuleMutationService still mutates binding fields directly: ExternalArtifactId, ExternalArtifactKind, MediaContentType, MediaOriginalFileName, MediaRelativePath, Route, StorageObjectReferenceJson
- HG-01 FAIL: writable metadata envelope still exposes foreign-owner IDs: ParticipantIds, MeetingNodeArtifactId, TranscriptNodeArtifactId, RecordingNodeArtifactId, LastProviderProfileId, ParentParticipantArtifactId, AssigneeParticipantArtifactId, RepositoryResourceId, SecretReferenceArtifactId, StorageCatalogId
- HG-02 FAIL: editable-node mutation paths still use ResolveHierarchyLinkKind / persisted hierarchy links.
- HG-02 FAIL: reparent path still persists hierarchy links.
- HG-02 FAIL: create/seed path still persists hierarchy links.
- HG-03 FAIL: workbench page still owns hardcoded capability rule 'ResolveNodeAssignmentRoles'.
- HG-03 FAIL: workbench page still owns hardcoded capability rule 'ResolveParticipantRole'.
- HG-03 FAIL: CRM/HR service still owns hardcoded node-role rule 'RequiresCanonicalNode'.
- HG-03 FAIL: CRM/HR service still owns hardcoded node-role rule 'IsAllowedNodeType'.
- HG-04 FAIL: provider UI still depends on legacy enum pattern 'Enum.GetValues<ProviderKind>()'.
- HG-04 FAIL: provider UI still depends on legacy enum pattern 'providerModel.ProviderKind'.
- HG-04 FAIL: resources UI still depends on legacy enum pattern 'Enum.GetValues<ResourceKind>()'.
- HG-04 FAIL: resources UI still depends on legacy enum pattern '@switch (editor.ResourceKind)'.
- HG-04 FAIL: resources UI still depends on legacy enum pattern 'editor.ResourceKind switch'.
- HG-04 FAIL: provider resolution still requires ProviderKind in the active adapter registry API.
- HG-05 FAIL: compensation pattern 'RestoreDeletedSubtreeAsync' is still present in the active cross-module mutation path.
- HG-05 FAIL: compensation pattern 'RestoreMovedDescendantsAsync' is still present in the active cross-module mutation path.
- HG-05 FAIL: compensation pattern 'MarkMutationCompensatedAsync' is still present in the active cross-module mutation path.
- HG-05 FAIL: cross-module mutation path still performs direct bridge-side work instead of durable intent execution.

Warnings:
- ADV WARNING: marker truth still appears dual (legacy scalar marker fields + marker set fallback).
- ADV WARNING: ProviderKind still exists. This may be acceptable only as a compatibility alias, not as the active control surface.
- ADV WARNING: ResourceKind still exists. This may be acceptable only as a compatibility alias, not as the active control surface.
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs' is still large (5002 lines > 4000).
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs' is still large (1159 lines > 1000).
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor' is still large (543 lines > 450).
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor' is still large (534 lines > 450).
```
