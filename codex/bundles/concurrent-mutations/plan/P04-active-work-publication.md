# Workbench publication with active-work accounting

The provider adapter has passed its repaired 103-case lane. The next dependency is
the Workbench publisher's interpretation of a planning decision: its v1 command uses
wall time since decision start, which would incorrectly charge a durable approval wait.

## Minimal change and compatibility

Add an explicit v2 source/command/result interpretation. Keep v1 source validation,
digest domain, encoded receipt fields, operation identity, lookup address and wall-time
budget behavior. Both versions keep normalization v1 and the same bounded task shape.
Reject mixed source/command/result versions and unknown versions. Receipt schema
already permits positive versions; no migration or new table is needed.

The digest uses a v2 domain only for v2 commands. It never includes elapsed work or a
remaining-duration observation: retries must retain the original semantic identity.
The existing owner receipt still stores exact accepted task identities and payloads.
Lookup/replay does not reacquire a live work budget or repeat a known effect; current
disclosure authority and target lifetime remain mandatory.

V2 requires an additional typed contract on the already-held publication authority:
`IProjectTaskPlanActiveWorkAuthority.RequireActiveWork(originalSource, originalPolicy)`.
It is a synchronous, local validation of the original live decision, not a supplied
duration or a new lock. Absence explicitly refuses before staging. The real Agent
adapter must implement it using the selected run/Agent lease and the owner-issued
monotonic capability; a test authority is only an oracle for the publisher perimeter.
Actual adapter/admission/saved-resume activation remains the subsequent integration.

The publisher checks this contract after acquiring the original authority, after the
project wait and material read, and immediately before SQL commit. It still compares
the complete bounded current material, checks original target lifetimes, current source
authority, dataset/epoch and wall-clock basis age. No file read, network/provider call
or human wait is introduced inside the SQL transaction. A work refusal rolls back all
task rows and the receipt and returns a definite no-effect outcome.

Permit a nonzero host replan ceiling only for v2 with the active-work contract. This
does not implement a replan in the publisher. The actual admitted coordinator must own
the original policy, spent work, bounded generation transition and replacement approval.
V1 retains its no-replan capability. A client cannot use v2 to select pinned publication
or bypass current material validation.

## Ownership and risk review

Edits stay in the existing Workbench planning contracts, codec, protocol, reader and
publisher. The additional contract remains in its existing authority file. No Core or
MAF dependency, project reference, store, transaction coordinator or partial is added.
Version selection is centralized in the source/protocol helpers so the reader and
writer cannot drift. Existing operation keys remain unchanged to detect a conflicting
reinterpretation under the same identity instead of creating a second receipt.

The critical risks are mixed version replay, charging approval time as work, trusting
a scalar elapsed value, and work expiry while waiting for a project lock or staging
SQL. The final authority check is already inside the transaction. Extend that check
with the typed work contract; preserve known-commit and unknown-commit semantics.

## Proof

Extend the existing pure operation-contract tests with v1/v2 round trips, distinct
semantic interpretation and strict mixed-version refusal. Keep the existing v1 cases.
Add PostgreSQL owner tests for an old decision with fresh basis/live work, missing work
authority, expiration after staging, stale material, wall-clock basis age, v2 replan
policy acceptance and exact replay without spending work again. Exercise the real
receipt reader and schema, not a test-only receipt.

Run the affected standard Release build, build-backed discovery, operation/currentness
regressions, CodeAnalytics dependency review and full portability-static enforcement.
Freeze the candidate before runtime proof and reconcile its existing shared PostgreSQL
fixture/resources afterwards. Maximum-size and sustained-capacity qualification remain
open; this change adds only local constant-time budget checks to the SQL path.
