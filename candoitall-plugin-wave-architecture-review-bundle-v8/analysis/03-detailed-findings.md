# Detailed findings

## P8-001 — Core node / binding boundary is still not sealed

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

### Why this is still a problem
The universal node carrier is still physically mapped with binding/media/artifact columns and the public metadata contract still exposes foreign-owner IDs. That keeps ownership blurry and invites future connector or CRM data to leak back into the core node record.

### Evidence
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:36-43 still declare Route, ExternalArtifactKind, ExternalArtifactId, MediaRelativePath, MediaContentType, MediaOriginalFileName, StorageObjectReferenceJson on ProjectObjectRecord.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:73-79 still map those fields to Workbench_ProjectObjects.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchLifecycleService.cs:85-88 and src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCommandService.cs:107-122 still directly assign binding fields on the node carrier before persisting.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:223, 234, 236, 244, 246, 290, 298, 300, 391, 447, 476 still expose foreign reference IDs on the metadata envelope.

### Required fix
Keep node as the universal carrier for identity, mindmap structure, XY, canonical markers, schedule, text, status, and subtype. Finish the separation by moving all binding/media/artifact/foreign-reference persistence behind binding/reference/facet records. Remove the binding columns from the mapped ProjectObjectRecord schema or make them transitional non-mapped runtime accessors only. Narrow metadata so foreign-owner IDs are not first-class writable payload.

### Closure proof
No binding columns mapped on ProjectObjectRecord. No direct binding-field mutation outside the binding/facet boundary. Foreign-owner IDs removed from the writable metadata envelope. Migration + tests prove old data still loads correctly.


## P8-002 — Hierarchy is still dual represented and dual written

**Severity:** Critical  
**Hard gate:** Yes  
**Repeat offender:** Yes

### Why this is still a problem
A node's parent relationship is still stored twice: once as ParentNodeKey and again as persisted hierarchy links. That means one structural fact has two durable owners. This is exactly the kind of drift source that will become painful under bulk refactors, plugins, agents, and cross-project movement.

### Evidence
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchRelationService.cs:83-110 reparenting updates ParentNodeKey and then persists a Contains/BelongsTo link.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:461-466 and 528-533 create hierarchy links when new editable nodes are created/seeded.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchGraphConventions.cs:33-37 still resolves hierarchy link kinds for persisted links.
- src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:71-73 explicitly says hierarchy links must come from the parent relationship, which confirms the link row is derived yet still persisted.

### Required fix
Choose one canonical hierarchy owner for editable nodes. The simplest path is to keep ParentNodeKey canonical and derive hierarchy edges in assembly/view models only. Generic link rows should remain only for non-hierarchy relationships. Delete editable-node Contains/BelongsTo persistence from create/reparent/move flows and add a data migration to clean historical duplicates.

### Closure proof
No persisted editable-node hierarchy links are created in create/reparent/move paths. Structure assembly derives hierarchy links from the canonical parent owner. Integration tests assert that editable nodes do not leave Contains/BelongsTo rows in Workbench_ProjectObjectLinks.


## P8-003 — Node-kind registry is not yet the authoritative capability matrix

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

### Why this is still a problem
The new registry is a real improvement, but assignment rules and canonical-node scope policy are still hardcoded elsewhere. That splits node semantics between the registry, the workbench page, and CRM/HR services. When plugins or agents start assigning parties or node capabilities dynamically, these seams will drift.

### Evidence
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:476-500 still hardcodes ResolveNodeAssignmentRoles and ResolveParticipantRole.
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4894-4933 still hardcodes RequiresCanonicalNode and IsAllowedNodeType.
- ProjectNodeKindRegistry already owns family, note-promotion, subtype mutation, visual profile, and metadata normalization; assignment/capability policy simply has not been pulled fully into it yet.

### Required fix
Extend the descriptor model so one registry resolves allowed party-assignment roles, required canonical-node scope, participant role interpretation, and other node-scoped capabilities. UI and CRM/HR validation must query the registry instead of shipping private switch statements.

### Closure proof
The hardcoded methods are removed. Registry tests cover allowed assignments and canonical-node scope. CRM/HR and workbench page both resolve their rules from the registry/capability service.


## P8-004 — Marker truth is still dual represented

**Severity:** Medium  
**Hard gate:** No  
**Repeat offender:** Yes

### Why this is still a problem
You explicitly treat XY and markers as canonical semantics of the mindmap, not just rendering hints. Right now markers still exist both as legacy scalar fields and as MarkerSet metadata. That means future analytics, cross-project similarity, and automated improvement logic can read different truths.

### Evidence
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:46-48 and 81-83 still persist MarkerIcon/MarkerTone/MarkerLabel.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:545-557 still resolves markers by falling back from MarkerSet to the legacy marker scalars.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchNodeMapper.cs:106 still merges both sources.

### Required fix
Keep markers canonical, but pick one durable representation. Recommended: keep a structured marker set on the node core and derive any primary-marker view fields from it. Migrate legacy scalar marker values into the canonical set and remove the fallback path.

### Closure proof
One canonical marker representation remains. Legacy marker scalar columns and fallback code are removed or fully retired. Migration + tests prove legacy markers survive the transition.


## P8-005 — Plugin platform exists, but provider/resource domains and UIs are still legacy-enum driven

**Severity:** Critical  
**Hard gate:** Yes  
**Repeat offender:** Partially

### Why this is still a problem
Connector manifests and plugin registries are now present, which is a big step forward. But the active provider and resource editor flows still branch on ProviderKind / ResourceKind enums and switch-based editors. That means email, LinkedIn, and custom API plugins are not yet truly first-class. They still have to squeeze through legacy enum categories or require core-page edits.

### Evidence
- src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10 still defines ProviderKind and src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:60-83 still resolves adapters through ProviderKind fallback APIs.
- src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor:252-253 still renders provider selection from Enum.GetValues<ProviderKind>().
- src/CanDoItAll.Modules.Resources/ResourceModels.cs:11 still defines ResourceKind and ResourcesService still persists it as a first-class domain field.
- src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor:48, 139-190, 425 still use Enum.GetValues<ResourceKind>() and switch-based typed editor rendering, even though ResourceConnectorPluginRegistry exists.

### Required fix
Move provider/resource editing to plugin-key + manifest/schema driven flows. Legacy enums can remain only as migration or classification aliases, not as the active resolution/UI branching mechanism. The UI should list connector manifests by capability/family, render config fields from schema, and save the selected plugin key without requiring core enum expansion.

### Closure proof
A synthetic plugin can be added without changing provider/resource enums or editing switch-based pages. Provider/resource pages no longer enumerate enums or branch on them for editor rendering. Resolution paths are plugin-key first.


## P8-006 — External-side-effect integration boundary is still not durable enough for the next plugin wave

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

### Why this is still a problem
The repo has mutation coordination and rollback/compensation for some internal cross-module operations. That is better than silent partial failure, but it is not the same as a durable outbox or connector-operation boundary. Email, LinkedIn, and custom API plugins will create external side effects, retries, approval flows, and idempotency requirements that compensation alone will not make safe.

### Evidence
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs:98-109 and 278-296 still rely on restore/compensate flows after a second mutation boundary fails.
- Search across src/ found no durable outbox / integration-event layer dedicated to connector side effects.
- The repo already has background-job infrastructure, which is a good foundation, but plugin-side effects are not yet modeled through it as durable intent records.

### Required fix
Before the email / LinkedIn / custom API wave lands, establish a durable connector operation boundary: canonical transaction commits intent, a worker executes connector side effects, retries are idempotent, and approval state is explicit. Internal compensation can stay where appropriate, but external side effects must not rely on same-request rollback semantics.

### Closure proof
Connector side-effecting operations use durable intent/outbox records and an execution worker with idempotency keys. Integration tests cover retry and crash-resume behavior. No direct side-effecting connector calls remain in request/transaction flows.


## P8-007 — Major service and file hotspots are still too large

**Severity:** Medium  
**Hard gate:** No  
**Repeat offender:** Yes

### Why this is still a problem
Correctness improved, but maintainability remains at risk. Several files are still large enough that architectural drift can hide inside them even when the public design is improving. This matters because the next wave will expand CRM/HR and connector surfaces further.

### Evidence
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs has 5002 lines.
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs has 1159 lines.
- src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor has 543 lines.
- src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor has 534 lines.

### Required fix
Split hotspot files by responsibility while the phase8 refactor is in flight. This should follow the canonical changes above rather than happen as a cosmetic move first.

### Closure proof
Primary hotspot responsibilities are separated and architecture tests/reference maps become easier to maintain.
