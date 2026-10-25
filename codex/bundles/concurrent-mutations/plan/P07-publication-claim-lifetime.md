# Process publication claim identity and lifetime

Status: implemented with [106 passing selected cases](../validation/2026-10-19-publication-claim-lifetime.md),
following signed workspace checkpoint 3360ad494. The design below preceded implementation.

The existing dispatch publication fence requires a running step, a non-null active token
and any claim with a live status for that step. That does not establish that the active
token has a matching attempt or an unexpired lease. A delayed publication could therefore
pass after expiry, or use an unrelated historical claim's status.

Keep historical child reading independent of dispatch permission. Extend the existing
exact child query with a nullable expiry projection for the claim matching the same run,
step, active token, attempt and dispatchable status. The claim identity is unique, so this
remains one bounded query. Preserve the existing any-live-claim predicate for manager
review, which must wait for claim release. Add no history scan, cache or new public port.

Inject the existing TimeProvider into the publication fence. After acquiring the root
transaction lock and reading the current child, dispatch requires a matching expiry
strictly later than the current time. Read the time after lock acquisition, so waiting
for another root transaction cannot preserve an expired observation. Keep the existing
ReadCommitted requirement, original grant/material checks and parent reconciliation lock.

Tests must cover expiry without a status transition, equality at the expiry boundary,
active-token and attempt mismatch, released status, valid renewal/rotation, and expiry
while blocked on the root lock. Historical reads remain available after refusal. Reuse
the existing deterministic Process fixtures with an explicit injectable publication clock,
and retain manager review, root independence, commit/rollback and bounded-query tests.

Build Persistence, Modules.Processes and Integration in standard Release; discover and
run the full publication-fence and canonical material-reader selections. Review scoped
CodeAnalytics changes, run complete portability-static, and retain exact source/runtime,
observer coverage and resource reconciliation before a signed checkpoint. This repair
does not activate an unsupported Process or Workflow producer.
