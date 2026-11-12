# Resource R1 failure-handling source amendment

Status: authored in the managed continuation checkout, unbuilt and unexecuted. This is
an explicit successor to the initial backend inventory
`57CE00C84E5EC4503FA8F3710AEA7B038C56586E6AD65DAB97360F3E7BC73066`;
that inventory and its original proof remain unchanged.

The independent source review found two failure-handling defects. Active-file observation
was outside the writer's precommit classifier, so an unavailable or Prepared activation
could escape as an exception before any owner transaction existed. Secondary logging
failures could also overwrite CleanupPending after an acknowledged database commit or
storage authorization release failure. The parent approved only these narrow repairs.

`ResourceMetadataWriter.ExecuteAsync` now observes the active binding inside its existing
try block before owner creation. An activation failure is therefore a sanitized typed
PersistenceUnavailable refusal; an actual namespace mismatch remains DatasetChanged.
Initial caller cancellation still propagates before dispatch. CommitAttempted remains set
immediately before CommitAsync, and no attempted outcome is downgraded by this change.
The read-only context/snapshot methods retain their existing exception contract.

CleanupPending now takes precedence over later logging/activity/catalog diagnostics.
Logging is still attempted, and known acceptance retains its exact snapshot. A promotion
revocation failure explicitly sets CleanupPending even if catalog publication or logging
already failed. A subsequent diagnostic failure cannot hide that cleanup obligation.
No public contract, durable receipt, additional owner or automatic retry was introduced.

Three existing proof arrangements were extended, with no new rows:

- The early-cancellation owner Fact also prepares a real attachment file through the
  activation service (which invalidates its cached binding). Save and delete must refuse
  before the paused owner factory is entered; zero commits and the retained Prepared
  marker are asserted. The test completes that same attachment and then checks the exact
  old Resource snapshot/Search and absence of the rejected create.
- The actual acknowledged commit/disposal/activity Fact now also makes diagnostics throw
  twice and requires CleanupPending plus the exact persisted snapshot and Search.
- The promotion cleanup and combined publication/cleanup/logging rows require
  CleanupPending while retaining the existing real grant-revocation and stored-file proof.

The owner class still has 12 + 6 Facts; promotion still has 3 Facts + 10 fixed inline
rows. The complete backend expectation remains 32 Unit + 80 Integration = 112 ordinary
rows, plus two separately owned browser fixture rows. These are source-derived counts,
not execution results. The prior model/migration, retained compatibility, PostgreSQL
concurrency/fault, native/browser, static and capacity gates remain open.

The runtime peer independently reviewed the five amended files and verified their exact
hashes. Its narrow source conclusion found no remaining blocker in these two fixes or
their extended arrangements. That review does not establish runtime behavior or waive
any validation gate above.
