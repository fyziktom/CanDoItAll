# W1b competing writer proof: source addendum

Status: authored only in the managed checkout. No build, discovery, test, database or
browser work was executed. This adds one disjoint test file and leaves the earlier
41-entry W1b source manifest unchanged. It replaces the earlier **unwritten source**
gap for deliberate writer overlap; actual proof remains pending.

`WorkspaceSettingsWriterOverlapTests` contains exactly two theories with two Boolean
InlineData rows each: intended discovery/runtime count **4 / 4**. The methods are:

- `Bootstrap_and_ordinary_save_observe_the_head_after_their_actual_exclusion_wait`.
- `Transfer_and_ordinary_save_preserve_the_actual_predecessor_and_only_change_owned_fields`.

| First holder | Positively observed waiter | Required result |
| --- | --- | --- |
| Actual bootstrap head exclusion; its existing provider-existence callback pauses after a real provider query | Ordinary save waiting on Workspace head table lock, blocked by that exact backend PID | Bootstrap repairs only provider preference; original human expectation is rejected HeadChanged; other six-field values stay original. |
| Actual ordinary SaveChanges flush, before commit | Bootstrap waiting on Workspace head table lock, blocked by the actual writer PID | Ordinary snapshot is accepted; bootstrap observes the newly selected real existing provider after waiting and leaves the accepted revision/values unchanged. |
| Actual transfer target SaveChanges flush, retaining lifecycle exclusion | Ordinary lifecycle SHARE read waiting behind the exact transfer backend PID | Transfer accepts the source provider only; stale ordinary command is rejected HeadChanged; other target values remain original. |
| Actual ordinary SaveChanges flush, retaining lifecycle SHARE | Transfer's pre-snapshot lifecycle EXCLUSIVE table lock waiting behind the exact writer PID | Ordinary save is accepted; transfer then preserves every accepted target field except its owned provider preference and advances revision again. |

The ordinary gate uses the existing injectable DbContext options/factory and a
SaveChangesInterceptor **after** the real flush. Transfer uses the existing
DatabaseTransferTestSupport owner contexts, so its actual target owner inherits that
same interceptor. Bootstrap uses its existing providerExists callback and an enlisted
context only to obtain the actual backend identity. There is no production test seam,
reflection, fabricated acceptance or replacement authority implementation.

The observer connection is opened before dispatch. Each arrangement polls the isolated
database's `pg_stat_activity` for the precise lock statement and confirms the expected
holder in `pg_blocking_pids`. A pending Task alone is not treated as lock evidence. The
production transaction/lock deadlines remain unchanged. A finite ten-second fixture
cancellation bounds the arrangement, and every finally releases both possible gates
and drains started operations before fixture disposal. All values and provider IDs are
synthetic. Existing TestApplication environments own and clean their leased databases;
no new server, container or global cleanup is proposed.

Migration backfill/restart proof, the performance matrix, generated SPA wire/caller
cutover and W1c durable outcomes remain separate unfulfilled gates. This addendum does
not convert authored tests into executed evidence.
