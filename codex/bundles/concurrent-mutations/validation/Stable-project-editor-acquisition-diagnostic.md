# Stable Projects editor acquisition failure — diagnostic continuation

Status: diagnostic patch integrated and its one-case focused execution passed on
2026-10-16. Root cause remains unresolved; no deterministic production repair or
whole-class confirmation is claimed. The result belongs to the frozen managed
candidate recorded in [its validation checkpoint](P02b-managed-candidate-validation.md).

The original qualified Stable Components run discovered/executed 3,090 cases:
3,089 passed, one failed, zero skipped, 2,927.15 seconds. Preserve its original
TRX at `.artifacts/concurrent-mutations/validation/p02bc-stable-qualified/CanDoItAll.Tests.Components.trx`
in the main checkout, SHA-256
`bbe72dab663e8db4aec5fbd4e2992a11546454a7c34621b37325e34fc1ef6cab`.
The parent-owned broad-run record remains unchanged by this continuation.

The failed case is
`CanDoItAll.Tests.Components.ProjectStructure.ProjectsEditorMutationTests.Reopened_same_target_cannot_mutate_while_the_original_seed_outcome_is_pending`.
It failed at the **first** `NewAsync` call, original line 388, before the save,
seed barrier, close or reopen. Original helper line 552 waited for
`project-name-input`: 81 checks and 87 component renders without finding it.
The test's total duration was 48.3374066 seconds; the repository already uses a
30-second default bUnit wait. These observations do not establish a seed race or
justify a longer timeout.

Source review confirms that the test awaits `ClickAsync`, the shared Button awaits
its callback, and `OpenNewProjectAsync` awaits native acquisition. The host either
installs a new scoped editor, ignores a superseded/canceled acquisition or catches
an acquisition exception and exposes `projects-read-error`. The independent
initial portfolio/package/pending refreshes do not invalidate editor generation.
Neither retained stdout/stderr nor the TRX contains the acquisition exception or
the rendered read-error state. No precise production correction is justified by
that evidence alone.

The managed-checkout patch changes only `ProjectsEditorMutationTests.cs`:

- Its existing controls receive `ILogger<ProjectsPage>` through normal test DI and
  retain error exceptions in memory. They emit no default logs and do not call the
  state formatter, serialize a draft, print SQL/parameters, or dump markup.
- `NewAsync` checks the completed acquisition immediately after the awaited click:
  new draft identity, open/editor mode, null project ID, captured scope, initial
  wizard step and one rendered name input. Failure diagnostics include those
  flags, submission readiness, mutation state, the visible read-error text and
  exception type/message observed during that call. The redundant selector-only
  30-second wait is removed; no timeout is expanded and no retry is added.
- All eight existing helper callsites pass their current controls. No Fact,
  Theory, InlineData, category or production behavior changes.

Expected targeted discovery/execution remains **one case** using exact FQN above.
The containing class remains **17 cases**, derived from **7 Facts plus 5 Theories
with 2 InlineData rows each**; it has no MemberData provider. After Stable releases
the engine lane, the parent owns build-backed discovery and a single-case rerun
to capture the actual cause. A deterministic repair, if established, then requires
the class's 17-case confirmation and applicable final portability enforcement.
The diagnostic was built, discovered and executed once (one passed), with the
candidate's final portability gate passing. The conditional 17-case confirmation
was not run because no deterministic correction was established. This narrow pass
does not erase the original failure or establish the broad suite is complete.
