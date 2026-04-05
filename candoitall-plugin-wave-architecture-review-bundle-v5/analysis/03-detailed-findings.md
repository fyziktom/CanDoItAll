# Detailed Findings

## PWA-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth

- Severity: `Critical`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388; 398-424; 1962-2239`

### Why this is bad

The structure and calendar loaders still call SyncGraphAsync, which writes system-managed nodes and links for Projects, Resources, Prompt Factory, Validation, and TestLab into Workbench tables. That keeps a mirrored graph in the same persistence store that also holds editable user-authored nodes. The result is parallel truth, unclear ownership, and future plugin drift.

### Required direction

Replace persisted sync with an assembly service that composes the surface from canonical editable nodes plus read-only module contributors. If persistence is truly required, move projections to dedicated read-model tables that are never used as canonical node storage.

## PWA-002 - ProjectObjectRecord is still an overloaded universal box instead of a stable carrier plus facets/bindings

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59`

### Why this is bad

The current record mixes node identity, semantics, route, artifact binding, media/storage, marker fields, schedule, and progress. Node can remain the universal carrier, but the carrier must stay lean and stable. As written, every new plugin or module has an incentive to add another field or another metadata convention into the same box.

### Required direction

Keep node as the universal carrier, but split non-core concerns into typed facets and binding tables. Keep semantic coordinates, markers, hierarchy, schedule anchors, and base textual meaning in the carrier. Move artifact bindings, media/storage references, and plugin-specific data out of the carrier.

## PWA-003 - Node-kind semantics are still fragmented across enum, subtype strings, canvas catalog, and editor mapping

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44; src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-90,123-159,225-377; src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:77-148,204-233,385-439`

### Why this is bad

A new kind or connector currently ripples through enum values, subtype strings, canvas catalog definitions, editor switch maps, and often other registries. That is tolerable for a closed list, but it is the wrong base for the upcoming plugin wave.

### Required direction

Introduce a central node-kind registry with descriptors, capabilities, editor schema, allowed relations, facet owner, and projection behavior. The UI and MCP layers should read descriptors, not hand-maintained subtype switch statements.

## PWA-004 - Node reclassification is still an in-place mutation without transition history or facet migration rules

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:949-1002`

### Why this is bad

The current ReclassifyObjectAsync mutates the existing row in place. That loses the semantic transition history the system actually cares about: brainstorming note to richer task, decision, or operational object. The current behavior preserves neither prior classification nor old facet payloads.

### Required direction

Keep node identity stable, but record semantic transitions explicitly. Add a transition history table and facet migration rules. For incompatible kind-family changes, archive the old facet snapshot and create a new facet instance under the same node.

## PWA-005 - Workbench metadata still carries cross-module references and opaque IDs that can become hidden canonical truth

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:202-323; 325-447; 613-648`

### Why this is bad

The metadata envelope still stores multiple foreign identifiers and cross-module references such as participant ids, resource ids, provider ids, and artifact ids. Even if some are currently “just helpers”, future modules and plugins will copy that pattern and turn metadata into a hidden second store of ownership.

### Required direction

Keep metadata only for node-local descriptive payloads. Move foreign ownership references into explicit facet tables or plugin data tables with typed boundaries and schema ownership. Tighten validation so metadata may not introduce new foreign-owner ids without an approved facet contract.

## PWA-006 - Workbench to CRM/HR reconciliation is safer than before but still non-atomic and compensation-based

- Severity: `Medium-High`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-749; 989-1135; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

### Why this is bad

The CRM/HR seam improved materially, but delete and move flows still persist Workbench changes first and then reconcile canonical assignments with compensation on failure. That is acceptable short-term, but the same pattern will become fragile once email, LinkedIn, and custom API plugins add more cross-module writes.

### Required direction

Before the plugin wave, choose a stronger cross-module mutation boundary: transaction where possible, or an outbox/saga orchestration with explicit recovery state. Keep the current compensation logic only as a transitional fallback, not the long-term model.

## PWA-007 - Plugin and connector architecture is still enum/switch/DI-registration based and not ready for the external integration wave

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-15; src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs:1-18; src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-44; src/CanDoItAll.Modules.Resources/ResourceModels.cs:1-90; 401-497`

### Why this is bad

Providers and resources are still modeled with enums, per-kind config switches, and hardcoded adapter registration. That works for a small fixed set, but it will not scale cleanly to email, LinkedIn, and custom API plugins with versioned configuration, permissions, health checks, and agent policies.

### Required direction

Introduce a plugin/connector manifest and capability registry. Plugins should declare schema version, secrets needs, capability set, health endpoint, node-kind hooks, agent policy exposure, and install/enable/disable/test semantics. The platform should consume descriptors instead of provider/resource enums.

## PWA-008 - Workbench service remains an oversized orchestration hotspot with too many reasons to change

- Severity: `Medium-High`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines); key behavior spans 343-424; 662-749; 944-1135; 1962-2239`

### Why this is bad

ProjectWorkbenchModels.cs is still a 3227-line hotspot that owns graph load, sync, CRUD, move/delete compensation, reclassification, calendar projection, and more. Even if each change is locally correct, this service shape will make the next plugin wave expensive and regression-prone.

### Required direction

Split the Workbench orchestration into separate load/assembly, node command, lifecycle, relation, and plugin-integration services. Keep the public surface stable while shrinking the number of change vectors in each class.

## PWA-009 - Hierarchy and semantic relations are still split between parent pointers and generic link rows

- Severity: `Medium`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:51; 90-109; 2158-2176; src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74`

### Why this is bad

Hierarchy is partly expressed via ParentNodeKey and partly via system-managed Contains/BelongsTo links. Even though user-authored hierarchy links are now forbidden, the system still emits generic link rows for hierarchy-like structure during projection sync. That keeps relation semantics fuzzier than they should be.

### Required direction

Make hierarchy canonical only through the explicit parent relation. Keep generic relation rows only for semantic links such as DependsOn, Blocks, Uses, Validates, and Tests. If projection contributors need tree edges for rendering, generate them in the assembled surface rather than persisting them as generic links.
