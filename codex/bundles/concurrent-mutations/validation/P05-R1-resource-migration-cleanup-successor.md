# Resource migration fixture cleanup successor

This source-only amendment preserves the original migration inventory `117DC7FCF0A8F76F210DC064C8CA48503185A1DF43A92D430D5A91D5AE9F3813` and its exact test image under `../reviews/P05-R1-resource-migration-cleanup-predecessors/`. The original SQL proposal, model expectations, migration suffix and three test cases remain unchanged.

The fixture factory now uses `TestFixtureCleanup.DisposeAfterFailureAsync` so a failed database lease disposal retains the setup/migration exception. The drift loop and lock-refusal assertion retain their primary failure and aggregate a subsequent schema-restoration or blocker-release failure. Blocker release still reaches a terminal result before the pending migration is drained in the inner `finally`; a cleanup fault cannot skip that drain. Cleanup is cooperative. No waiter-only timeout is represented as completed cleanup, and the external test runner remains responsible for a hung process deadline.

The pending migration exception is already the subject of the lock-refusal assertion. Its terminal observation during cleanup does not assert success or replace a failed barrier/assertion with an expected lock exception. There is no retry of the migration or disposal.

Exactly three `[Fact]` methods remain. The combined proposal stays at 346 ordinary rows and four separate browser rows; source counts are not discovery or execution evidence. No build, test, database, production source, schema, migration generation or main-checkout change was performed for this amendment. The remaining validation gates in the original proof continue to apply.

Independent review is recorded in the successor inventory; the original inventory is historical and is not rewritten.
