# P02a request and native recovery review

Scope: current full/scalar definition entrypoints, native Projects draft lifetime, comparison
and recovery, and the new two-context browser test. This is source review, not an execution
report. The coordinating agent's `P02a-validation.md` records the executed 31/31 PostgreSQL
cases (17 owner and 14 HTTP, zero skipped). Those results precede the final native refinements
and do not certify their component/browser behavior.

## Request and accepted-result boundaries

`Api/ProjectEditorSaveRequest.cs` distinguishes missing/null full-replacement collections
from explicit empty arrays. Its model conversion shares only request-local collection
objects; `ProjectsService.SaveWithReceiptCoreAsync` copies scalars and every child into typed
immutable commands before its first await. `ProjectDefinitionWriter.Prepare` normalizes
these into independent read-only arrays before transaction acquisition. A caller editing
its original model while database work is pending cannot alter the admitted payload.
This does not promise support for concurrent mutation of a List while synchronous capture
itself is executing; native Blazor dispatch is serialized and captures before awaiting.

The scalar agent adapter constructs `UpdateProjectDefinitionScalarsCommand` before waiting
for the lease. The owner never fills a missing original expectation from a later read.
The coherent reader returns a repeatable-read root/children snapshot and a nonempty original
revision; an absent definition is NotFound. The legacy editor-template read remains a
compatibility API, and the legacy full-save HTTP route returns an ID, not a durable receipt.
New scalar writes return the exact transaction acknowledgement. Deterministic stale,
missing and unchanged-target transaction failures retain distinct error codes.

Native capture separates submitted values from live rows and field revisions. The accepted
phase/option map is validated in full before changing the model: indices, identity uniqueness,
original persisted identities, required phases and meaningful option coverage. Omitted blank
persisted options lose their old ID without losing later typed text. Every accepted existing
save must advance the original revision. No fresh read substitutes for this acknowledgement.

Final review repaired loss of accepted metadata after a client mapping error. The host now
records `ProjectEditorAcceptedOutcome(Submitted, Accepted)` and the accepted project ID
before applying the acknowledgement. `Acknowledgement` means validated/applied state;
`AcceptedOutcome` independently retains the exact received revision, fields, child mapping
and original submission. A rejection leaves the live model unchanged and enters the terminal
`SaveReconciliationFailed` state. Actual seed-in-progress failures use `SeedOutcomeUnknown`.
Neither state admits blind retries. The existing malformed-map test now asserts both atomic
draft state and exact outcome retention; the held native-save test asserts retained origin.

## Comparison, recovery and retained input

Only the host invokes `GetDefinitionAsync`. It binds each comparison read to the draft,
editor generation and cancellation source. Close, replacement, dispose and a later intent
prevent late success or error from updating another editor. A failed read retains the draft
and allows an explicit retry. Same project/profile identity is checked; a changed lifetime
can be adopted only by the operator's explicit new-draft recovery.

Comparison does not change original expectation, raw input, field revisions or EditContext.
Reload deliberately discards the draft. Manual reconciliation starts from every value and
child ID in the inspected current snapshot and keeps previous drafts available for copying;
it never attaches a fresh token to the old payload. All previous model fields and local
starter-plan strings remain reachable through one selected predecessor and Older/Newer
controls. Rendering is bounded to that predecessor plus the current saved comparison, while
the explicitly retained chain lasts until accepted reconciliation or editor close.

Native date inputs require a precise limitation: an incomplete browser date can report
`badInput` while exposing an empty `value`; Blazor can separately retain unparseable text
outside the DateTime model. There is no existing Projects RawInputs contract. Manual recovery
therefore refuses to retire the editor while browser or Blazor date validation is invalid.
It leaves the original control mounted and asks the operator to complete or clear it. Explicit
discard remains available. The renderer reuses the existing inspect/focus JS module, guards
the originating draft after each await, and confirms the same comparison record still applies
before invoking reconciliation. Existing light/native/browser cases exercise the refusal and
subsequent explicit correction; the repeated-conflict case verifies old plans remain reachable.

## Dependency and fixture review

No new references were added. Projects.UI still depends on BaseLib and Projects.Contracts;
the new comparison injects no services and cannot read, write, navigate or manufacture an
expectation. ProjectsPage retains effect authority. The synthetic sandbox exposes a reachable
DefinitionConflict scenario using the same renderer and callbacks. Four rendering register
entries were reviewed: ProjectsPage, ProjectModalHost, sandbox Projects, and the new comparison.
Components MCP was unavailable; exact shared source and existing usage were inspected, as
recorded in `P02a-native-editor.md`.

The browser test uses the existing LiveUiHost and owned PostgreSQL profile. The first context
belongs to the host; the second is explicitly await-disposed before host teardown. Both have
1920×1080 viewports, independent storage/circuits and startup-prompt completion. It does not
copy authentication storage or silently log in as another person: both exercise the existing
trusted local operator host path. This is two-editor concurrency proof, not two-principal
authorization proof. The fixture must own its server/profile; an external BASEURL is not a
substitute because LiveUiHost requires OwnedDatabaseProfile for seeding. No new Docker stack,
credential, persistent browser profile or global cleanup is introduced.

Browser assertions wait for concrete accepted/conflict UI, inspect persisted owner values and
IDs, and check both circuit/browser oracles. Only the conflict and manual-reconciliation
views produce screenshots when executed. Failure screenshots and the host log aid diagnosis;
actual geometry, focus, assets and screenshot review remain execution requirements.

## Remaining proof and scope

- Refreshed discovery/builds and execution for the final native candidate: lightweight
  38 source cases (focused 23), native ProjectsEditorMutationTests 17, and the new browser
  case (one of two ProjectsPortfolioBrowserTests cases). Counts are expectations until
  build-backed discovery; this reviewer ran none of them.
- The malformed acknowledgement is injected at the pure draft seam, not by replacing the
  production owner inside the host. The host's separate known-commit catch was reviewed
  directly; no fake persistence result is claimed as database proof.
- Parent-owned regression repairs, performance comparison, final complete portability scan,
  rendering-register enforcement and documentation validation remain required.
- Durable operation/replay identity, crash recovery, expiry/tombstones and mandatory atomic
  search projection are P02b. Whole-engine owner coverage and agent/process freshness policy
  remain later planned work; P02a must not be presented as whole-engine completion.

No remaining source blocker was found in this focused perimeter after the two retention
repairs. Closure is conditional on the final execution and static gates above.

Parent execution subsequently discovered and passed the 38 lightweight, 51 native
and one browser cases, inspected both browser screenshots and passed final static
checks. See `P02a-validation.md` for exact evidence and the retained performance issue;
the review's P02b scope limitations still apply.
