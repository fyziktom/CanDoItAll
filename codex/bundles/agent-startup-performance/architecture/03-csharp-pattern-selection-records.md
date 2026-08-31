# Pattern Selection Records

## P01: Operation-local facts

Force: repeated policy construction performs expensive I/O. Choose explicit internal operation-scoped fact reuse, not a global caching pattern. Simpler public factory cache is rejected because it changes every consumer and can retain stale case/root facts. New type only if a typed operation value is clearer than existing parameters; no new project or interface. Tests count probes while adversarial root/ancestor changes still fail.

## P02: Query projection and validation separation

Force: revision probes build effective runtime profiles/catalog copies. Choose a typed database projection plus reuse of existing validation, separate from full materialization. Reject token-only caches, distributed memoization and new repository abstractions: they weaken validated availability or add a layer without an independent responsibility. Shared publication canonical/structural validation remains; local validation parity must be proved before narrowing. Test malformed unchanged-token inputs and supported mutation invalidation.

## P03: Explicit immediate/recovery trust boundary

Force: a freshly prepared plan is reread/revalidated like a recovered journal. Choose a locally typed/privately controlled immediate path using existing prepared plans; recovery remains untrusted. Reject a globally toggleable skip-validation flag and all log batching. Existing generic-new-run immediate/recovery split is a precedent, not permission to skip every check. Tests compare projections and prove every recovery failure boundary with real writes.

No builder/factory/strategy framework or broad architecture extraction is justified. If local changes become impossible without a new public boundary, reopen planning before proceeding.
