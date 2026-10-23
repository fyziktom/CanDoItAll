# P26: project Process launch and original-intent recovery

Status: qualified, 2026-10-23. [Validation](../validation/2026-10-23-project-process-launch-client.md)
records nine backend cases, 135 SPA tests and fourteen final browser states. Behavioral
proof tier; no whole-engine closure. Prerequisites:
signed backend P25 `8b630649c` and SPA `707bcc57b`; their qualified bounds remain
unchanged. SPA work stays on `codex/concurrent-mutations` for colleague review.

## Ownership and decisions

The project Processes placeholder becomes a launch workspace. The existing Process
catalog, authoring lifetime admission and prepared-launch store remain authoritative.
Add an optional project ID to the four definition reads so project publications and
overlays are selected by the existing projection owner. An omitted ID preserves the
global catalog. Empty IDs are rejected; a typed missing-project exception maps only
that condition to 404. Existing Process read permission still governs these reads;
selection is never an execution grant. No new project reference, persistence schema,
runtime service or cross-module query is introduced.

Add one read-only lookup by caller intent, backed by the existing indexed store lookup
and existing caller/profile/surface verification. It returns the existing observation
shape and never prepares, resumes or dispatches. Reuse the status mapping with the
admission lookup. A missing intent is an observation at that instant, not proof that
an earlier request cannot complete. This read is necessary after an unacknowledged
preparation: replaying a fresh check in a different dataset must not silently prepare
a replacement. After finding the original admission, the existing check with that
admission restores its exact saved review, including policy and settings provenance.

The SPA owns selection and a small explicit session state machine, separate from its
rendering. Select a project definition, a discovered planning contract (or ordinary
execution), and Workspace defaults or an advertised read-basis choice. Show the actual
returned plan, assignments, warnings and effective policy before explicit acceptance.
The initial surface accepts no arbitrary variables or prompt text. Eligibility remains
the check endpoint's responsibility; unsupported definitions fail visibly.

Persist one bounded pending record per browser tab before any check or acceptance:
version, project, original request, intent, optional admission and whether acceptance
was attempted. Do not persist review content, credentials or runtime results. Storage
failure blocks dispatch. Validate restored storage and refuse malformed/unknown data.
Never overwrite another pending project or silently forget an ambiguous acceptance.
Reload and navigation invalidate asynchronous UI effects; recovery starts with an
authorized read, then restores only a found original admission. No automatic writes,
polling or background retries. Explicit recovery preserves the original request even
if catalogs/defaults change. Same-intent acceptance retries use the same execution
choice and admission. Acknowledged acceptance shows the original run and continuation;
failed/reconciliation states remain visible, not success. A deliberate separate launch
is allowed after acknowledged acceptance, or an explicit discard when acceptance was
never attempted. A check cannot start a run, including when its response is unknown.
An attempted acceptance cannot be discarded merely because a later read says Prepared.

All mutations use the generated client through a fresh bounded single-attempt transport.
The renderer uses existing SPA components and stays at the project route; backend UI
routes are not assumed to be SPA routes. Route changes dispose the old session and late
results cannot alter a new workspace. Native Blazor presentation is outside this slice:
its required Components MCP is unavailable and its projection boundary is unchanged.

## Cost, risks and validation

Project reads reuse existing catalog work and lifetime checks. Recovery costs a bounded
number of indexed preparation reads and at most the existing run-state observation;
there is no history scan, held transaction, worker, timer or subscription. The existing
global catalog is not paginated; this change neither adds fan-out nor claims a new
capacity bound. Browser storage is size bounded, tab local and contains only selection
identifiers. It is recovery evidence, not authority. Server verification remains required
after every reload; missing or forbidden recovery retains the original record.

Before closure: build changed production and owning test projects in Release, confirm
exact discovery, and run focused SQL/HTTP cases for scoped overlays, missing/deleted
projects, permissions, caller isolation, original-intent observation and unchanged
launch/publication behavior in both snapshot modes. Export real OpenAPI, inspect route
and schema deltas, regenerate the SDK, and test actual query/response serialization.
Session tests hold requests to prove lost responses, storage failure, immutable retries,
route disposal, concurrent actions and changed catalogs/defaults. Render the final UI at
1920x1080 and inspect selection, saved review, unknown outcome, recovery, refusal and
accepted/pending states. Browser fixtures and backend SQL proof are reported separately
unless a real joined end-to-end run is performed. Run full portability enforcement,
review source/output identities and resource reconciliation, update durable docs, and
sign both checkpoints. No broad Stable claim or whole-engine closure follows from this
slice; the requirements ledger retains all remaining owners and lifecycle work.
