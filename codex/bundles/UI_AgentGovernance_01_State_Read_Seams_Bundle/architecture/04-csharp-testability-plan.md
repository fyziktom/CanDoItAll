# Testability and proof boundaries

G00 adds public tests against the **current** real panel/page/workspace contract before new implementation types exist. Exact intended facts and their expected current status are in the [semantic map](../plan/semantic-test-map.md). Build, list compiled cases, freeze FQNs/arguments, then run RED. A setup failure, missing class or compilation failure is not a semantic witness. Already correct agent-generation fencing and missing-ID behavior remain characterizations.

G01 unit tests exercise deterministic session transitions through public methods and controlled TaskCompletionSource reads (including noncooperative completions and synchronous returns). Component tests drive InputSelect and actual row/retry buttons, use public parameter echo/removal, capture callbacks and verify visible lane states. No private reflection, uninitialized production objects or exact partial/file-count tests. Cancellation proves suppression of owned UI publication, not rollback of backend work.

G01 integration constructs the production read adapter and registered current-profile workspace against the repository DB fixture, seeds existing store records, and proves agent filtering, Take/order, detail identity and cancellation. At least one persisted detail includes approvals/artifacts/checkpoints/receipts/timeline/metrics. No external provider invocation, synthetic desired-outcome fake or existing tracking test alone substitutes for canonical read proof.

G02 renders the real service-free surface without production registrations. Verify complete actual sections, escaped text, badge precedence, empty/loading/stale/error, independent immutable collections, controlled selection and one intent per action. Page tests prove access/selection publication for current target only. No approval mutation is introduced.

G03 tests the moved real children through both Governance and AgentRuntimeDetailsDialog. Sandbox query tests preserve absent/unknown Catalog default and existing Capabilities/Overview state; real browser checks use the controlled snapshots and native components. A collection copy test protects the public presentation contract, not private implementation shape.

Current test locations and method names are source inventory in ../inventory/existing-tests.json; sealed Overview proof is historical. New verification has no execution results yet. Counts are frozen per run for unexpected discovery detection, never as architecture invariants.
