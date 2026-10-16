# P02a native editor implementation and proof plan

Status: source prepared; no build, test run or browser screenshot is claimed by this record.
The coordinating agent owns serialized Release validation and the final portability gate.

The native Projects editor records a typed conflict with its frozen submitted command and
retains the live draft, EditContext, child row identities and raw input. Reading the current
definition is explicit and guarded by the editor generation; it never adopts an expectation
into the old payload. A failed or retired comparison read cannot replace the draft.
Discard/reload constructs a new draft from the inspected snapshot. Manual reconciliation
constructs the same current-based draft and retains the old draft for comparison/copy until
the reconciled save is accepted. Neither action performs a write or an automatic merge.

Successful owner results are recorded as committed and registered by their accepted project
identity before applying the acknowledgement. A malformed acknowledgement leaves the draft
unchanged and the host in `SaveReconciliationFailed`, with same-target writes blocked.
The separate `AcceptedOutcome` retains the complete received acknowledgement and original
submission even when the validated/applied `Acknowledgement` remains unchanged.
`SeedOutcomeUnknown` is reserved for failures after starter-object seeding began.

The existing Wide dialog remains the work surface. Its primary editor and saved-definition
comparison use the shared Grid, Stack, FormSection, TextBlock, Alert and Button wrappers.
The comparison includes all editable scalars, external identity, phase goal/status/dates,
and option category/name/notes. Manual reconciliation also shows previous local linked-object
plans for selective copying; none are implicitly recreated. The `DefinitionConflict`
sandbox scenario exercises the same surface and explicit callbacks without a database.
No CSS, package or stat-card surface was added.

Previous drafts are retained in memory until an accepted reconciliation or editor close;
Older/Newer controls render one selected predecessor at a time. No retained predecessor is
silently discarded after a further conflict. Manual recovery refuses to retire a form with
invalid Blazor or browser date input: native date controls do not expose their incomplete
displayed fragments as a serializable value. The form remains mounted with guidance to
complete or clear those dates; explicit discard/reload can discard them. This is a stated
retention constraint, not a claim that unparseable browser fragments were copied to a model.

The Components MCP tools were absent from the available-tool catalog. The Components skill
and compact UI composition guide were read; exact existing component source was inspected
under `CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components` (Layout/Grid and
Stack, Forms/FormSection, Typography/TextBlock, Feedback/Alert, Buttons/Button and
Modals/Dialog), together with current Projects usage. This is source inspection, not MCP
or rendered browser proof.

Source cases added for this recovery slice:

- Lightweight: one draft-state fact and one real-renderer fact for explicit actions,
  complete comparison values and linked-object plans, disabled save and preserved invalid
  date input; one actual sandbox host fact proves comparison/recovery do not write and the
  subsequent explicit save does.
- Native host: two cases for discard versus reconcile, including held stale save, later
  typing, raw invalid date, failed comparison retry, current child preservation and a
  successful explicitly reconciled save; one late-read case after editor retirement.
- Browser: one deterministic two-context test in `ProjectsPortfolioBrowserTests`, using
  the existing isolated LiveUiHost and two 1920×1080 browser contexts. It verifies conflict,
  raw invalid input retention, manual reconcile, accepted child identities, subsequent
  stale conflict and explicit discard/reload. Screenshots are emitted only when executed.

Focused lightweight filter:
`FullyQualifiedName~ProjectDefinitionContractTests|FullyQualifiedName~ProjectEditorDraftTests|FullyQualifiedName~ProjectEditorSurfaceTests|FullyQualifiedName~ProjectsBoundaryTests`.
Use build-backed discovery for the authoritative count before recording results. Native
filter is `FullyQualifiedName~ProjectsEditorMutationTests`; browser case is
`FullyQualifiedName~Two_browser_contexts_preserve_conflicting_drafts_and_require_explicit_current_based_recovery`.

P02b remains responsible for durable operation identity and receipt recovery. This editor
change adds no automatic write retry, force-save or recovery-lookup claim.
