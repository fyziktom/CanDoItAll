# Deletion owner-completion retry presentation

Source-only stage-2 native checkpoint. Parent authorized the additive
`ProjectDeletionRecovery.OwnerCompletionUnconfirmed` caller change; backend ownership,
SQL release-fault proof and API mapping remain with the persistence author. This slice
does not change Resource metadata, C3, migrations, shared registrations or owner APIs.

## Behavior and ownership

The existing host still records the typed partial deletion as a confirmed deletion and
retires the matching editor. It retains the real participant/recovery IDs supplied by
the owner. When owner completion is unconfirmed, the notice states confirmed deletion
and requires operator review. An empty failure list does not create a synthetic cleanup
row. A retry whose typed result requires owner review also retains its already selected
exact target, even when no remaining participant failure is returned.

`ProjectDeletionCleanupState` is an internal, page-owned state/mapper. It retains blocked
target observations separately from current pending reads. A later read that permits
retry, omits the target, or follows an ordinary partial result cannot clear this local
knowledge. Generic unconfirmed retry failures use the same sticky hold with distinct
guidance. There is no automatic read, recovery command, owner callback, or block-reset
API in this helper. The state is page-instance scoped; this is not durable P03 recovery
across a new page or process.

The optional presentation `RetryBlocked` defaults to false. It is distinct from the
existing `CanRetryNow`, whose false value describes an active claim and has historically
permitted an explicit retry to observe that claim. Ordinary false-default owner results
keep that behavior. The existing Button is disabled for a retained hold or busy target.
Its callback rechecks current rows/holds/busy state, including a queued callback captured
before a render change. The host independently checks its current state before invoking
the owner. Draft/editor lifetime, ordinary selection and other renderer layout remain
unchanged; no CSS or shared library changes are needed.

## Source proof and selection

| Owning assembly | Exact selector | Rows | Traits |
| --- | --- | ---: | --- |
| Unit | `FullyQualifiedName~CanDoItAll.Tests.Unit.Projects.ProjectDeletionCleanupStateTests` | 2 | none |
| Projects.UI.Tests | `FullyQualifiedName~CanDoItAll.Tests.Components.ProjectsUi.ProjectDeletionStatusSurfaceTests` | 8 | none |
| Components | `FullyQualifiedName=CanDoItAll.Tests.Components.ProjectStructure.ProjectsPageTests.Partial_project_deletion_closes_stale_editor_and_exposes_explicit_cleanup_retry` | 1 | HostPlatform |

This is 11 disjoint focused rows, with three new Facts: two state/mapper Facts and one
renderer Fact. The renderer class changes from seven to eight Facts. The existing real
host/owner normal-retry Fact is unchanged. The full `ProjectsPageTests` class remains
26 Facts plus one two-row InlineData theory = 28, if broader host validation is needed.
No selected skips, dynamic data, LiveProcess, browser or LongRunning rows are added.

The new state cases cover two participant IDs sharing one recovery GUID, missing
preparations, empty owner failure lists, inventory omission/reappearance, late normal
observations, immutable target identity, ordinary claim-refresh behavior and generic
unknown retention. The renderer case exercises the actual Button/renderer callback,
blocked display and exact target, a previously captured callback after blocking, normal
false-default dispatch, and callback refusal after row removal. These are deterministic
state and component assertions; they do not substitute for actual owner disposal faults.

## Guarded transfer and validation limits

The source inventory names six source/test files, this note and a two-row rendering
patch. Existing managed predecessors and their exact main counterparts were recorded
before editing under the ignored `project-deletion-retry-predecessors` ownership root.
The page's original main/managed byte hashes differ only by line endings; its textual
diff was empty. Preserve main line-ending policy or apply the reviewed semantic hunks.
The rendering patch carries exact old/new LF hashes; apply only its two rows rather
than copying the managed register or the earlier Resource row patch wholesale.

Only targeted source reads, source-count inspection, hashes and `git diff --check` were
performed. No build, tests, browser, host, database, migration, broad scan or commit ran.
Independent persistence peer reviewed all six source/test files with no blocker. The
review binds the stable public recovery contract `ProjectDeletion.cs` at SHA256
`30A338A9C574A21216D090F4A609D19671CCFD29F5730792EC89D966EDE5748A`.
Parent must bind the final backend recovery contract, build the affected Projects/UI
and test projects in standard Release outputs, verify exact discovery and run the 11
rows above. Full portability-static no-write enforcement and the rendering register
gate remain required at the parent checkpoint. Actual browser behavior and durable
operator recovery are not claimed by this narrow source proof.
