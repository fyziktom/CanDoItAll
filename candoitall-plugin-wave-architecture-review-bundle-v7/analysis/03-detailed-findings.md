# Detailed findings

## P7-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth

- Severity: `Critical`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-001`
- Area: `Canonical model / Workbench assembly boundary`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:398-425; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2239`

### Why this is bad

Structure, calendar, and other flows still materialize Projects, hierarchy, Resources, Prompt Factory, Validation, and TestLab data into Workbench canonical tables through SyncGraphAsync. That means Workbench storage still mixes real project-owned nodes with mirrored projection nodes. This blocks a clean plugin platform because future connectors would inherit the same anti-pattern.

### Required direction

Remove persisted SyncGraph-as-storage behavior from Workbench canonical tables. Keep project-owned nodes canonical, but assemble cross-module read-only surfaces through contributor services or explicit read-model tables outside Workbench_ProjectObjects / Workbench_ProjectObjectLinks.

### Required closure proof

No SyncGraphAsync method or call remains in Workbench read flows; no system-managed cross-module nodes are persisted into Workbench canonical tables; new assembly contributor layer and guardrail tests exist.

## P7-002 - The universal node carrier is still overloaded instead of being a stable carrier plus typed facets and bindings

- Severity: `Critical`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-002`
- Area: `Node carrier / Facets / Bindings`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:143-177; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-244`

### Why this is bad

ProjectObjectRecord still mixes identity, hierarchy, text, route, external artifact binding, media, storage reference, progress, marker columns, metadata, and scheduling in one broad record. The node should stay the universal carrier, but the carrier itself must stay lean or every new module and connector will keep expanding it.

### Required direction

Keep node identity stable and central. Keep canonical text, status/priority, semantic X/Y, canonical markers, and schedule anchors on the node carrier. Move artifact ids, media/storage payload, provider/resource/secret bindings, and kind-specific business payload into typed facet or binding tables keyed by node identity.

### Required closure proof

ProjectObjectRecord no longer owns external artifact/media/storage binding fields; typed facet/binding tables exist; X/Y and canonical markers remain available as canonical node semantics.

## P7-003 - Node-kind semantics and node-scoped capability rules are still fragmented and hardcoded

- Severity: `Critical`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-003 + PW6-009`
- Area: `Node-kind registry / Capability matrix / CRM-HR node scope`
- Evidence: `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44; src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-120; src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs:1-529; src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs:1-197; src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:217-243; src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4888-4933`

### Why this is bad

Node meaning is still split across ProjectObjectType, ObjectSubtype strings, create catalog definitions, editor mapping, and CRM/HR hardcoded role checks. This prevents clean extensibility for new connector-driven block types and for richer node-scoped assignments to people, agents, and partners.

### Required direction

Introduce a central ProjectNodeKindRegistry / descriptor model with family, allowed relations, allowed party roles, editor schema, transition rules, facet owner, and command exposure. Create/edit/reclassify/UI/CRM-HR node scope validation must all consume this registry.

### Required closure proof

A node-kind registry exists; page code no longer hardcodes ResolveNodeAssignmentRoles/ResolveParticipantRole; CRM-HR no longer hardcodes RequiresCanonicalNode / IsAllowedNodeType for node-scoped roles.

## P7-004 - Node reclassification still mutates in place without transition history or facet supersession

- Severity: `High`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-004`
- Area: `Node lifecycle / Transition history`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:949-1002`

### Why this is bad

ReclassifyObjectAsync still mutates the same row in place and the integration tests still validate that behavior. That preserves stable node identity, which is good, but it loses the semantically important evolution from quick note / brainstorm capture into richer operational structures.

### Required direction

Keep node identity stable, but write explicit ProjectNodeTransitionHistory and facet migration / supersession rules. Shared carrier fields may stay mutable, but kind transitions must be journaled and kind-specific facet payload must be archived or superseded instead of silently overwritten.

### Required closure proof

Reclassification writes transition history, preserves stable node identity, and adds guardrail tests for same-family and cross-family transitions.

## P7-005 - Hierarchy is still dual-represented through ParentNodeKey and generic link rows

- Severity: `High`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-007`
- Area: `Hierarchy / Relation invariants`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:447-499; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:626-650; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1059-1068; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2286-2289; src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74`

### Why this is bad

Create, seed, reparent, and subtree move flows still write canonical parent assignment and also persist Contains/BelongsTo link rows for the same hierarchy. Even though user-authored generic hierarchy links are forbidden, the storage model still duplicates the tree in two places.

### Required direction

Choose one canonical containment model for editable nodes. Prefer ParentNodeKey (or a dedicated canonical tree table) as the single truth. Keep the generic relation table for semantic edges only: DependsOn, Blocks, Uses, Validates, Tests, DerivedFrom, and similar non-containment semantics.

### Required closure proof

Editable create/reparent/seed/move flows no longer persist hierarchy links; guardrail tests fail if Contains/BelongsTo is reintroduced for canonical editable nodes.

## P7-006 - Workbench metadata still carries foreign identifiers and keeps dual marker truth

- Severity: `High`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-006`
- Area: `Metadata boundaries / Marker semantics`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:219-247; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:287-331; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:388-477; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:545-585`

### Why this is bad

Metadata envelopes still contain cross-module ids such as participant, artifact, provider, resource, secret, and storage references. Markers are also represented both through legacy columns and metadata marker sets. This invites hidden canonical truth to leak back into metadata again.

### Required direction

Keep descriptive node-local payload in metadata only. Move foreign ids and reusable bindings to explicit canonical tables. Keep X/Y and markers canonical, but collapse markers to one canonical representation instead of legacy columns plus metadata fallback.

### Required closure proof

Foreign-id helper fields are removed from metadata envelopes or clearly moved into binding tables; marker storage has one canonical representation only; guardrail tests cover both constraints.

## P7-007 - Provider/resource/connector architecture is still a closed enum-and-switch seam

- Severity: `Critical`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-005`
- Area: `Plugin platform / Connectors / Providers / Resources`
- Evidence: `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-63; src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-48; src/CanDoItAll.Modules.Resources/ResourceModels.cs:10-81`

### Why this is bad

Workspace and Resources still rely on ProviderKind and ResourceKind enums, closed adapter registration, and per-kind switch logic. That is not a viable base for email, LinkedIn, and custom API connectors with descriptors, config schemas, secrets, health checks, capabilities, and node hooks.

### Required direction

Introduce a manifest/descriptor-driven connector platform. Profiles should bind to connector keys and schema descriptors, not to closed enums. Resources and providers should become first-party plugin descriptors using the same extension seam.

### Required closure proof

ProviderKind/ResourceKind are no longer the extensibility seam for new connectors; connector descriptors/manifests exist; new first-party connectors register through the descriptor platform.

## P7-008 - Cross-module mutation boundaries are still compensation-based and not ready for outbound connector side effects

- Severity: `Medium-High`
- Gate: `Conditional blocker`
- Status: `Open`
- Repeated from: `PW6-008`
- Area: `Mutation orchestration / Transactions / Outbox`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-748; src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1038-1133; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`

### Why this is bad

Delete and move flows still persist Workbench changes first and reconcile CRM/HR afterward with rollback-on-failure logic. That is survivable internally, but it is not a safe base once email, LinkedIn, or custom API plugins begin performing outbound or externally visible actions.

### Required direction

Before allowing connectors to perform outbound or destructive side effects, introduce an explicit mutation boundary: single-transaction orchestration where possible, otherwise durable outbox/saga patterns with replay and recovery state.

### Required closure proof

A documented and tested mutation boundary exists; outbound connectors are forbidden until that boundary is in place.

## P7-009 - Workbench and CRM/HR service hotspots remain too large and multi-responsibility

- Severity: `Medium`
- Gate: `Watch`
- Status: `Open`
- Repeated from: `PW6-011`
- Area: `Service decomposition / Maintainability`
- Evidence: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines); src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs (5001 lines)`

### Why this is bad

ProjectWorkbenchModels.cs is still over 3200 lines and CrmHrServices.cs is still over 5000 lines. Even when local changes are correct, future plugin wave changes will be expensive and regression-prone if these seams keep accumulating behavior.

### Required direction

Split orchestration by reason-to-change: assembly, node commands, lifecycle/history, relations, facets/bindings, assignments, party directory, and connector-facing orchestration.

### Required closure proof

Hotspot files are decomposed or at least the new extension seams isolate future connector work away from the hotspots.

## P7-010 - There is still no hard architecture closure mechanism preventing the same blockers from being reintroduced

- Severity: `Critical`
- Gate: `Hard blocker`
- Status: `Open`
- Repeated from: `PW6-012`
- Area: `Architecture guardrails / Closure proof`
- Evidence: `Current static review still matches the unresolved issues from prior bundles; No dedicated ArchitectureGuardrail test suite was found in tests/; No repo-level hard-gate script enforcing closure of repeated blockers was found`

### Why this is bad

The same blockers were already called out in earlier bundles and are still present. Without explicit hard gates, forbidden-pattern checks, and dedicated architecture guardrail tests, Codex can keep improving local code while leaving the structural blockers alive.

### Required direction

Add architecture guardrail tests plus a repo-level hard-gate script. No bundle item may be closed by ADR-only justification; closure requires code search proof, required tests, and the hard-gate script passing.

### Required closure proof

Architecture guardrail tests exist; a hard-gate script passes on the refactored branch; bundle closure requires the script output and test names as evidence.
