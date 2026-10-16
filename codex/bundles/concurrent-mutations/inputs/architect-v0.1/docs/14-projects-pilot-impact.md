# 14 · Projects pilot: concrete change-impact map

## Scope anchor

The pilot owns the definition currently edited through ProjectEditorModel: project scalar fields, phases and options. It does not make the entire project graph, conversations, usages and runs one revision. Exact current source anchors are SRC-03, SRC-06, SRC-07, SRC-08, SRC-10 and SRC-15. P00 must resolve all actual API/page/model clone/caller locations after current component extraction.

## End-to-end impact

| Area | Necessary change or proof | Consequence for consumers/tests |
|---|---|---|
| Canonical schema | Add reviewed content stamp and any chosen owner receipt storage/mapping | Migrations, complete AppDbContext model, ProjectsDbContext parity, retained datasets |
| Project and child writes | All covered child-only changes participate in root revision atomically | Direct child methods, seed/import/transfer, raw/bulk SQL, compensation must be reviewed |
| Editor read | Return coherent project/phase/option snapshot plus original edit stamp | UI clone, API DTO, tool read output, missing-vs-create behavior |
| Owner save | Require original stamp on update; validate lifetime and source authority; return exact accepted state identity | Existing Result<Guid> adapters may need a richer compatible owner result |
| Child identity | Reject duplicate/foreign/unknown persisted IDs under the new contract; map temporary/new rows | Preserve row identity after creation and typing during save |
| Public HTTP | Enforce expected condition, define errors and operation lookup | OpenAPI/XML/JSON examples, generated clients, API-only UI |
| Agent tool | Retain original read stamp through request/proposal/runtime to same owner | Tool schema and version compatibility, no prompt-only token rebinding |
| UI host | Freeze submission, retain live draft, handle conflict/unknown/committed-warning | View contracts, sandbox state, EditContext and request generation tests |
| Search | Selected transactional participant or durable ordered rebuild | Failure semantics, lock ordering, latency, deletion and delayed callback tests |
| Activity/audit | Keep optional feed clearly separate from durable result/audit guarantee | No falsely rejected save due to observer or read-back failure |
| Create/reservation | Bind one accepted creation/lifetime/reservation to retry identity | Duplicates and postcommit-lost response cannot create twice |
| Deletion/recreate | Stale handles cannot target new incarnation | Receipt retention, child cascading/retained-history rules |
| Import/transfer/restore | Allocate local stamps and correct dataset namespace; historical records remain inert | v2/v3 package compatibility, restoration/activation and old-session behavior |
| Composition | Register new mechanics without a new fact owner or UI dependency | Evaluated project graph and owner-context architecture tests |

## Detailed accepted-result contract requirements

Return accepted project identity, lifetime and content stamp; operation identity/status; any new phase/option durable-ID mapping; and enough normalized accepted data to reconcile the dispatched submission accurately. Do not report a later GetAsync snapshot as the exact original commit result without signalling the intervening change.

If the public current API must temporarily retain its old return shape, add an explicit versioned contract or a deliberate adapter. That adapter still must make the outcome/recovery path available and preserve precondition enforcement. Merely dropping the richer fields and telling the client to "try again" is not parity.

## Existing semantic details that require an explicit decision

The editor materializes default option categories that need not all exist as rows. Blank options are skipped by the current save path. CurrentPhase is free text rather than a foreign key to the phase list. Slug derives from Name. UpdatedAt changes on save. Existing GetAsync returns a new draft when a specified ID is missing. These behaviors are observed in SRC-03; some may be intentional and must not be silently changed during token addition.

For the new protected edit read, recommended behavior is to distinguish explicit create from missing existing project, so a delayed deletion cannot look like a successful load of a new object. Record any change in API/UI compatibility and test it. Keep the current normalization/default-category meaning unless reviewers intentionally adjust it.

Collection identity validation may reveal callers that relied on unknown IDs silently producing new rows. Map and upgrade those callers rather than preserving accidental duplication. Canonical import is a distinct maintenance command, not a reason for ordinary editor save to trust foreign child IDs.

## Migration outline, not an executable script

Drain existing protected writers and background producers. Apply the canonical schema migration with per-live-object stamp initialization and any selected receipt constraints. Upgrade all active entry points. Reject old update contracts instead of guessing stamps. Reopen editors/read new state. Reconcile retained operations under the documented epoch/lifetime rule. Run new and retained-data tests before restoring general writes.

No independent schema initialization is added to ProjectsDbContext. No destructive reset of developer data is required by this architecture. The exact upgrade/recovery command is prepared later against the current migration head and the user's environment.

## Pilot closure is a vertical proof

Two clients load one project. The first adds a phase; the second changes the title and tries to save the old complete snapshot. The second must not erase the phase. Repeat using the actual agent tool instead of a direct service call. Repeat with child-only change, delete/recreate, lost response, duplicate request, post-dispatch typing and delayed search work.

The accepted solution must also demonstrate a conflict-free ordinary save, normal create, valid source-authorized mutation and explicit lifecycle rejection. Negative tests alone can hide an implementation that simply rejects all writes.

## Cost and coupling limits

No new dependency from Projects to Process/Agent implementations; keep the existing Projects-owned source port and Workbench adapters. No validation path may call a complete hierarchy/structure dashboard just to compare one stamp. No single shared mutable draft/receipt cache becomes the owner. No external side effects enter the retriable database section. Search integration must use an existing intentional owner port, not direct access to Search tables.
