# Detailed Findings

## PW6-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth

- Severity: `Critical`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388; 398-425; 1767-1833; 1962-2240`

### Why this is bad

Structure, calendar, and command flows still call SyncGraphAsync, which materializes Projects, Resources, Prompt Factory, Validation, TestLab, and hierarchy projection data into Workbench canonical tables as system-managed nodes and links. That leaves one store for editable canonical nodes and a second mirrored truth in the same storage model.

### Required direction

Remove persisted SyncGraph-as-canonical behavior. Introduce an assembly boundary with per-module projection contributors. Canonical Workbench storage must hold only real project-owned carrier nodes; read-only module surfaces must be assembled or moved to explicit read-model tables.

## PW6-002 - ProjectObjectRecord is still an overloaded universal carrier instead of a stable carrier plus facets and bindings

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59`

### Why this is bad

The carrier still mixes identity, text, hierarchy, route, external artifact binding, media and storage payload, progress, markers, schedule, and arbitrary metadata. Node should stay the universal carrier, but the carrier itself must stay lean and semantically stable or every new module will keep expanding it.

### Required direction

Keep node as the universal carrier. Keep identity, hierarchy anchor, kind key, primary text, semantic X/Y, markers, and schedule anchors on the carrier. Move external artifact ids, media/storage details, provider/resource/account references, and plugin-specific payloads into typed facets and binding tables.

## PW6-003 - Node-kind semantics are still fragmented across enum values, subtype strings, canvas catalog, and editor switch maps

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44; src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-120; 225-377; src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:45-180; 385-439`

### Why this is bad

A new kind or connector currently ripples through ProjectObjectType, ObjectSubtype conventions, ProjectStructureCanvasCatalog definitions, and ProjectStructureNodeEditor switch maps. That is the wrong extensibility model for the upcoming email, LinkedIn, and custom API plugin wave.

### Required direction

Introduce a central node-kind registry with descriptors, family, allowed relations, allowed assignment roles, facet owner, editor schema, command exposure, and transition rules. UI and MCP layers should consume the registry, not scattered switch logic.

## PW6-004 - Node reclassification is still an in-place mutation without transition history or facet migration rules

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:948-1002`

### Why this is bad

ReclassifyObjectAsync mutates the same row in place. That keeps the same node key, which is good, but it discards the semantically important evolution from brainstorming note to richer task, decision, or other operational block. There is no transition history and no facet archival or migration contract.

### Required direction

Keep node identity stable, but write explicit transition history and facet migration/archival rules. Same-family reclassification may version the active facet; cross-family changes should supersede the old facet and activate a new one under the same node.

## PW6-005 - Plugin, provider, and resource architecture is still enum/switch/DI-registration based

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-63; src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-48; src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs:1-17; src/CanDoItAll.Modules.Resources/ResourceModels.cs:1-82; 401-497`

### Why this is bad

ProviderKind, ResourceKind, hardcoded adapter registrations, and per-kind serialization switches still dominate the integration model. That works for a tiny closed set, but it is not a stable base for external connectors like email, LinkedIn, and custom APIs with versioned config, secrets, health checks, and policy surfaces.

### Required direction

Introduce a connector/plugin manifest and registry model. Providers and resources should become first-party plugins using descriptors for config schema, secrets, capabilities, health, commands, node hooks, and agent policy exposure.

## PW6-006 - Workbench metadata still carries foreign identifiers and cross-module references that can become hidden canonical truth

- Severity: `Medium-High`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:223-246; 290-330; 391; 476`

### Why this is bad

Metadata still contains participant, artifact, provider, resource, and storage ids. Even if some of them are currently treated as helpers, future feature work will copy this pattern unless the architecture moves these references into explicit binding ownership.

### Required direction

Keep node-local descriptive data in metadata only. Move foreign ids and reusable cross-module bindings into typed binding/facet tables with clear owners and schema validation.

## PW6-007 - Hierarchy is still dual-represented through ParentNodeKey and generic link rows

- Severity: `Medium-High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:483-500; 646-655; 2286-2289; src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74`

### Why this is bad

User-authored creation and reparent flows still write ParentNodeKey and also persist generic hierarchy-like links. Canonical hierarchy should live in one place. Generic link rows should represent semantic relations only, not duplicate containment.

### Required direction

Make hierarchy canonical only through the explicit parent relation (or a dedicated canonical tree table if you prefer that shape). Keep the generic relation table only for semantic edges such as DependsOn, Blocks, Uses, Validates, and Tests.

## PW6-008 - Workbench to CRM/HR lifecycle reconciliation is still compensation-based and non-atomic

- Severity: `Medium-High`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:703-747; 1088-1128; src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4684-4749; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

### Why this is bad

Delete and move flows persist Workbench mutations first and reconcile CRM/HR assignments afterward, with compensation on failure. That is safer than before, but it becomes fragile once more modules and plugins join the same mutation boundary.

### Required direction

Choose the long-term mutation boundary now: transaction where possible, otherwise durable outbox/saga orchestration with explicit recovery state. Keep compensation only as a transitional fallback.

## PW6-009 - Node-scoped assignment semantics are still incomplete; only two roles are validated against canonical node type

- Severity: `High`
- Plugin-wave blocker: `Yes`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4888-4925; 4928-4933; src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:15-31`

### Why this is bad

Today only MeetingParticipant and WorkItemAssignee require canonical node validation. But the product direction already includes assigning people, agents, and partners to nodes such as tasks and decisions. Without a central role-to-node capability matrix, future CRM/HR and plugin work will drift into ad-hoc rules.

### Required direction

Define assignable node capabilities in the node-kind registry. All node-scoped assignment roles must be validated through that capability model, not through a tiny hardcoded RequiresCanonicalNode switch.

## PW6-010 - Node scope resolution and persisted assignments still collapse to raw NodeKey strings and persisted Workbench rows

- Severity: `Medium`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs:8-68; src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-149; src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs:413-428; src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4994-5002`

### Why this is bad

The cross-module boundary uses ProjectNodeReference, which is good, but resolution still queries persisted Workbench rows directly and assignments still store a raw string NodeKey. That will get in the way of assembled read-only projections and any future non-Workbench contributor nodes unless the canonical-only scope model is made explicit.

### Required direction

Keep the typed ProjectNodeReference boundary. Tighten resolution so only canonical assignable nodes are legal node scope targets. If the string storage format remains, hide it fully behind canonical resolution and capability enforcement.

## PW6-011 - Workbench service remains a large orchestration hotspot and CRM/HR has a second major hotspot

- Severity: `Medium`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines); src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs (5001 lines)`

### Why this is bad

ProjectWorkbenchModels.cs remains a 3227-line multi-responsibility hotspot, and CrmHrServices.cs is now over 5000 lines. Even if each local change is correct, the next connector wave will become expensive and regression-prone if these seams are not decomposed.

### Required direction

Split Workbench into assembly/load, node command, lifecycle, relation, and binding services. Split CRM/HR integration and assignment orchestration from the rest of CrmHrServices. Preserve public contracts while reducing reasons to change per class.

## PW6-012 - Architecture guardrail tests still do not cover the most important canonical invariants

- Severity: `Medium`
- Plugin-wave blocker: `Watch`
- Status: `Open`
- Evidence: `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425; architecture/adrs/ADR-0004-workbench-node-extension-guardrails.md`

### Why this is bad

There are many useful integration tests, but there is still no dedicated guardrail layer proving no persisted projection truth, no hierarchy duplication, no hidden foreign ids in metadata, no invalid role-to-node assignments, and no enum-driven plugin additions.

### Required direction

Add architecture-level guardrail tests that fail as soon as someone reintroduces parallel truth, hierarchy duplication, metadata foreign-id leakage, invalid node-role bindings, or enum/switch-only plugin expansion.
