# 5. Project Structure and safe module contributions

## Three distinct layers

**Native authoring** owns notes, local objects, placement, links, and metadata. **Work Management** owns task meaning, dates, dependencies, and assignments. **Projection** composes foreign entities/relations. They may share a physical project/store initially while retaining distinct mutation rules.

Creator is not owner. An agent-created note belongs to Structure; actor/run is provenance. A published workflow artifact remains in the workflow manifest; Structure owns its reference/placement.

| Kind | Canonical fact | Allowed change from Structure |
|---|---|---|
| Simple note | Inline native body | Typed note edit with revision. |
| Folder/block/local annotation | Native structure/metadata | Owner command with structural invariants. |
| Task/WorkItem | Work Management, possibly the same native record | Task command, never generic internal task-field patch. |
| File/image/video node | Structure binding plus Storage object/version | Explicit create/upload/content replacement; Notes is description only. |
| Agent occurrence | Agents definition; local placement/alias | Alias locally; technical edit through Agents. |
| CRM person/resource occurrence | CRM identity/staffing facts | Local view; authorized CRM edit. |
| Workflow definition node | Workflow/version; Structure input binding | Revision-checked binding choice; definition edited at owner. |
| Process/run projection | Runtime source state/manifest | Read/hide/link or owner control action, not run-state patch. |
| Prompt/resource/test reference | Respective source owner | Attach/detach/annotate, source edit at owner. |
| Meeting/recording/transcript/participant | Native metadata or explicit external reference, by actual kind | Do not infer recording/STT from a scaffold; bytes are Storage, parties CRM. |
| Repository/runtime/environment/infrastructure/link | Native descriptor or connector reference, by kind | Typed validate/execute/open/configure/test capability, no arbitrary root/secret grant. |
| Secret reference | Structure reference; Security secret | Detach locally; revealing/deleting secret requires separate authorization. |

SimpleNote body is a semantic fact, **not an instruction to introduce a new column**. Existing Notes may be canonical content for the note kind while being description for a file kind. No rename/migration without a concrete reason.

## Relationships

Structure owns author-created links even when endpoints are projections, but cannot edit those sources. Projects owns project hierarchy, Work Management owns work dependencies, runtimes own execution edges. Every relation kind specifies owner, endpoint kinds, direction/cardinality, cycles, scope, and deletion behavior. Related-to does not silently become scheduling dependency. Cross-project links require explicit kind support and access to both scopes; no phantom master is created for rendering.

## Safe contribution protocol — CON-021

A producer adapter resolves trusted source authority and frozen origin, then invokes the Structure owner with typed intent and stable identity. Structure revalidates target, lifecycle, kind, relevant revisions, quotas, and policy. New content is staged in authorized Storage. Native effects and receipt are committed atomically, with an outbox only where durable downstream publication is required. Content finalization is reconciled according to the driver's actual semantics. No open page is required.

| Input | Meaning |
|---|---|
| Operation identity | Stable logical intent, not transport attempt; trusted producer establishes the namespace. |
| Producer namespace | Registered identity, not model-selectable privilege. |
| Origin | Agent/Workflow/Process run, step/iteration/output key where applicable; human operations have their own intent. |
| Target binding | Frozen project, parent NodeKey, binding revision and destination role; not current selection. |
| Kind/action | Closed typed intent or registered versioned schema; no EF/SQL/service-name/privileged flags. |
| Content | Native body, task draft, EntityReference, or bounded staging/content reference. |
| Expected revisions | Modified target and critical invariants; leases do not replace concurrency. |
| Fingerprint | Canonical kind, target, content hash/reference, meaningful options; exclude rotating lease/token/timestamps. |
| Requirement | Optional/Required obligation in the stored orchestration plan; never a grant. |
| Authority | Internally resolved delegation/purpose/scope, not trusted arbitrary JSON. |

Supported target intents include native-note/node creation for allowed kinds, task creation, entity/asset attachment, managed-asset creation, structural links, revision-checked contribution updates, and explicit withdraw/detach. Preserve narrow existing APIs rather than create a giant universal union. Updates distinguish producer-controlled fields from later user changes.

**Required** means the caller cannot declare the corresponding business obligation complete until applied, legitimately compensated, or explicitly waived by authorized policy. Denied, deleted, and quota-exceeded destinations remain blocked/pending or failed as appropriate. No admin escalation, alternative parent/project, or textual pretend-success is permitted.

## Durable idempotency and user edits

Unique identity includes relevant scope/data incarnation, registered producer, stable intent/origin, and output/action key. Validate the caller's namespace. Same key and semantic fingerprint returns the original receipt; changed content/target conflicts. Reauthorize disclosure before returning a historical receipt.

Commit native effects and receipt together; check-then-create without durable uniqueness is insufficient. In-memory locks can optimize but cannot establish restart/multi-instance correctness [SRC-025]. Content staging has its own stable identity and bounded reconciliation/retention. A replay after commit retrieves the existing effect; it does not overwrite subsequent human changes.

If a user deleted the result, return AlreadyAppliedTargetDeleted/Suppressed with safe evidence, not a replacement. A new legitimate restore/intent is distinct from a producer changing keys to evade suppression. Receipt retention must cover supported replay windows; expired history is not automatic permission to execute again.

## Managed lifecycle

Ordinary results remain normally editable under policy. A managed projection can support hide rather than source deletion. Kind and management are fixed by trusted policy and visible descriptors; a producer cannot convert a note into an undeletable system object.

An invariant managed slot has an explicit authorized repair/reconcile operation with audit. Read, redraw, or producer restart does not have that authority. A visible missing/broken state is safer than silently defeating the user's decision.

## Several effects, content, and failure

A Structure-owned node/link set may be atomic when its API promises this. Mixing CRM changes, runtime admission, or Storage requires explicit coordination. Report completed, pending, failed, and compensated effects. Preserve intentionally partial multi-root deletion rather than normalize it into an invented all-or-nothing result [SRC-005].

Possible semantic states include Applied, AlreadyApplied, AcceptedPending, Conflict, Denied, TargetNotFound/Deleted, Suppressed, UnsupportedKind, QuotaExceeded, InvalidContent, CapabilityUnavailable, and NeedsReconciliation. APIs need only relevant states, but cannot equate missing acknowledgement with failure or rollback. Required outputs must remain discoverable after a restart; unsupported durability in a legacy path is a documented gap, not a retroactive guarantee.

Storage success is not SQL success and vice versa. Mark PendingContent where applicable; do not expose a completed file before bytes exist. Input text and returned descriptions remain untrusted data. Bound size, graph fan-out, kind schema, and target access.

## Preserve existing adapters

IProjectStructureRuntimeGateway, Structure tools, and ProjectStructureWorkflowExecutor must be adapted or relocated, **not deleted as undesirable coupling** [USER-001, SRC-027]. Task/file authoring is required parity. Structure contributions are one specialization of the module-operation protocol in [17](17-operation-protocol-and-multi-owner-journeys.md), not the only destination automation may use.
