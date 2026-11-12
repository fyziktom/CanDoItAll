# Stage 2 source-review corrections

This successor preserves inventory `EFE97FFC` and its two replaced source images in
[the amendment predecessor record](../reviews/P05-R1-deletion-compliance-predecessors.json).
No runtime or test count changes are inferred from review.

`ProjectsApi.cs` removes the newly authored XML guidance and trims the old unconditional
retry/participant-only claims that no longer describe all 409 outcomes. The existing
typed response, model and runtime code are unchanged. The maintained
[deletion completion contract](../../../../docs/architecture/concurrent-mutations/09-project-deletion-completion.md)
states the operator-review rule, current-authority requirement and pending-list limit.
OpenAPI/client generation remains an integration gate; no SPA or authentication proof
is claimed by the authorization-disabled HTTP fixture.

The new `FreshSnapshot` partial keeps ten Facts. Its three competing-owner finally
paths now drain the pending operation even if holder disposal fails, before context
disposal. Fixture creation failure attempts both scope and application disposal and
retains the initial and cleanup exceptions. These are terminal cooperative awaits;
the outer owned process deadline remains the escape for a hang.

Fault arrangements now retain the actual backend PID in `TransactionStarted`, before
the injected exception can hide the transaction result. For first/owner Begin,
prelude-end and lost-unlock acknowledgment paths that require quarantine, a separate
context polls fresh autocommit `pg_stat_activity` observations until that PID is absent,
with a ten-second cancellation budget and a delay between observations. This assertion
runs while the failed owner context still exists, after the independent keys-free
assertion. Lost session-acquisition acknowledgment followed by confirmed reverse unlock
may retain a healthy pooled backend; that arrangement does not require physical exit.
No pool is disabled and no server backend is terminated by the test.

The assertions add prospective evidence for physical quarantine behavior; they have
not been executed. Selected source counts remain 30 deletion-owner, 2 HTTP, 7 compensation
Unit and the unchanged other lanes: 132 disjoint cases overall, 14 new Facts across
stages 1/2/native. The original inventories, Resource test two-hop predecessor, native
subset `F8750903`, stage-1 successor `182F4784`, and stage-3 runtime gate remain intact.
