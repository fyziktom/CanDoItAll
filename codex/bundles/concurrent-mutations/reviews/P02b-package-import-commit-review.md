# Package import commit knowledge review

Source review, 2026-10-16. This review did not execute builds, tests or external
effects. Parent owns candidate validation. The reviewed patch is a maintenance
import repair, not a durable package operation protocol.

`DatabaseTransferOperationRunner.RunProfileAsync` records Unconfirmed immediately
before calling the exclusive import transaction's commit and Acknowledged only
after that call returns. Its outer catch covers owner-session retirement,
transaction/connection cleanup and maintenance-context disposal. Therefore a
server commit followed by an acknowledgement exception remains Unconfirmed;
later cleanup cannot turn an acknowledged commit into an ordinary precommit
failure. `DatabaseTransferImportScope` retains its primary exception first when
cleanup also fails; the outer knowledge variable survives an aggregate exception.

`ProjectPackageService.ImportAllAsync` records the imported result or original
unconfirmed package identity before propagating the typed commit exception.
Both paths preserve staged bytes. The outer service catch retains that knowledge
through storage-plan retirement and extraction-directory disposal, including
cancellation exceptions after the commit boundary. Compensation is restricted
to the path that never reached that boundary. Acknowledged cleanup failure
returns original record/file counts plus CleanupIncomplete; uncertainty returns
ImportOutcomeUnconfirmed without claiming imported counts. Current storage-plan
retirement itself has no disposable provider resources or throwing operations.
No blocking backend defect was found in this trace.

The native handler captures its own path/target request before awaiting. Later
selector edits cannot redirect the running import or its acknowledged message.
Both CleanupIncomplete and ImportOutcomeUnconfirmed hold subsequent package
dispatches. A concrete evidence-loss defect was repaired: target-list refresh
failure now uses the existing read-error alert with independently owned
packageTargetsError, rather than overwriting the import result. Successful target
refresh clears only that read error. Changing the selected target, path or dialog
visibility does not clear the hold.

The hold and displayed evidence are **page-instance state**. Navigating away and
recreating the page, a new tab, browser/circuit restart or another client loses
that local safeguard. A late completion updates only the disposed original page;
there is no durable package lookup or shared target fence in this repair. The
empty-target owner checks remain useful but cannot resolve an uncertain earlier
dispatch. Do not claim navigation-safe or cross-restart package recovery until
P06 supplies its owner-specific durable intent, original target binding and
resolution policy. No new durable framework was added here.

Source proof selection:

- `ProjectTransferHistoryPersistenceTests.ImportCommit.cs`: four package rows
  cover failure after flush before commit, true server commit followed by lost
  acknowledgement, connection cleanup after acknowledgement, and publication
  failure. Two row-import rows cover commit ambiguity and acknowledged cleanup.
  Fresh target reads and bound file contents establish actual effects; the
  publication fault alone is not a database acknowledgement-loss proof.
- `ProjectsPageTests.Package_import_cleanup_warning_or_unknown_outcome_preserves_evidence_and_blocks_repeat`:
  two rows retain warning/unknown evidence, then change current target/path,
  fail a later target refresh and assert the original message and dispatch hold
  remain. Directly invoking the callback still performs only one import.

The six backend rows do not separately inject maintenance-context disposal,
extraction deletion or a simultaneous primary plus cleanup failure. Those paths
were inspected through the enclosing knowledge scopes; do not report their
runtime coverage as tested by these rows. Parent must record actual discovery
and execution, including final portability/rendering enforcement.
