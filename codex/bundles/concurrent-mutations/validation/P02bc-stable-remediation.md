# Frozen foundation Stable run and pending repairs

The parent started `p02bc-stable-qualified` against main commit
`702d0a93b1081c42d5a06893e7070219aee5d089` on 2026-10-16. Main and the
referenced sibling sources/binaries were frozen through final reconciliation. Source
repairs below are isolated in the managed runtime checkout and have not been
built, discovered, executed or scanned.

Product and Stable Release builds passed with zero errors. Exact canonical-filter
discovery matched all 36 independently counted assemblies: 18,588 discovered
cases, with 18,643 expected runtime rows after the seven reviewed deferred theory
expansions. Execution started at 19:37:01 UTC. All 36 assembly commands ended;
Integration was interrupted after the diagnosed stall described below.

The Unit assembly executed all 10,285 expected rows: 10,282 passed, three failed,
zero skipped. Original evidence remains in
`.artifacts/concurrent-mutations/validation/p02bc-stable-qualified/CanDoItAll.Tests.Unit.trx`
in the main checkout. No failed run has been replaced or automatically repeated.

| Failure | Reviewed source repair | Validation still required |
| --- | --- | --- |
| `ProjectLifetimeSerializationTests.Package_project_json_keeps_saved_identity_and_business_fields_while_restoration_gets_a_new_lifetime` | The new internal root revision leaked into ordinary entity JSON. Mark `Project.ConcurrencyToken` ignored by JSON, consistent with the lifetime token. Explicit `ProjectDefinitionExpectation.Revision` remains the supported wire witness. Strengthen the existing test to require fresh nonempty revisions on materialization and omission from raw entity JSON. The actual profile/package transfer DTO already omits live revision and creates a new root revision. | Existing serialization case, owner definition tests, HTTP expectation tests and affected transfer/package cases. |
| `DatabaseCanonicalityArchitectureTests.Profile_specific_db_context_factory_is_limited_to_explicit_maintenance_boundaries` | Add the reviewed offline `DatasetActivationCommand` to the exact-file maintenance inventory. It uses the complete context only for migration of the explicit selected PostgreSQL profile; no product module gains the context or an exception. | Existing exact-path and compiled module-boundary tests. |
| `ProjectsWorkbenchDbContextTests.Projects_model_contains_exactly_the_six_owner_records` | Keep the exact owner inventory, adding state, epoch and definition receipt records. Require exactly one concurrency token, on `Project.ConcurrencyToken`; retain rejection of CRM and Workbench records. Rename the test to describe the owner boundary. | Existing Projects/Workbench model cases and model/migration parity. |

These edits change no test row count. They do not establish a passing Stable run.
The first focused repair selection is source-counted as seven Facts: one in
`ProjectLifetimeSerializationTests`, four in `DatabaseCanonicalityArchitectureTests`
and two in `ProjectsWorkbenchDbContextTests`; there are no dynamic data providers.
Use the three full class filters joined with `|` and require exactly seven
discoveries before execution. Broader owner/HTTP/transfer and model-parity proof
listed above remains separate from that narrow repair selection.

The Components assembly then executed all 3,090 expected rows: 3,089 passed, one
failed, zero skipped. Native command duration was 2,927.15 seconds. The failure
is `ProjectsEditorMutationTests.Reopened_same_target_cannot_mutate_while_the_original_seed_outcome_is_pending`:
`NewAsync` could not find `project-name-input` at line 552, called from line 388,
after 81 checks and 87 component renders. Its retained TRX SHA-256 is
`bbe72dab663e8db4aec5fbd4e2992a11546454a7c34621b37325e34fc1ef6cab`.
The cause and a deterministic repair are under review; no timeout increase or
rerun has been authorized as a substitute for that investigation. Integration
execution began after this completed lane. The elapsed time is a serialized
local suite observation, not a concurrency throughput measurement.

## Reconciled partial result and Integration stall

The corrected retained reconciliation accounts for **16,199 executed rows:
16,182 passed, 17 failed, zero skipped**. Thirty-three assemblies passed all
cardinality, original display-label multiplicity, test/execution identity and
outcome checks. Unit and Components failed as recorded above. Integration
executed 1,255 of its expected 3,699 rows: 1,242 passed and 13 failed. Its remaining
2,444 rows were not executed. The broad gate is **IncompleteOrFailed**.

Two owned `dotnet-stack` captures, at 21:38:15 and 21:39:34 UTC, showed the same
workspace-service/receipt-provider construction cycle in the direct fake-runtime
test-host composition. The production current-profile wrapper defers workspace
construction, so this is not evidence that every production host deadlocks.
Receipt recovery nevertheless had an unnecessary dependency on mutation admission.
The reviewed repair extracts its existing receipt-only behavior into
`ProjectDefinitionToolReceiptRecovery`; the direct fake-host regression additionally
rejects any renewed mutation-admission construction. See the
[composition repair](../reviews/P02b-receipt-recovery-composition-repair.md).

After retaining PID/start/parent ownership and both captures, the parent stopped
only the original testhost at 21:39:55 UTC. Integration exited 1 after 4,145.25
seconds. The runner then completed both remaining Memory assemblies. No stalled
test was retried. Its remaining database/directory ownership is being reconciled
before further database execution.

The thirteen Integration failures comprise six Simple Chat transfer activation
fixtures, one Provider History transfer activation fixture, one canonical model
inventory, two API description coverage rows, two obsolete delayed-search fixture
rows and one retained-created-target fixture. Their repairs retain explicit
activation, original lifetime evidence, exact model ownership and actual
postcommit acknowledgement races; no assertion is replaced by a broader exception
or a longer timeout. Each requires fresh build-backed focused discovery/execution.

## Retained postprocessing amendments

Original runner SHA-256 remains
`003a6429282de5d4886475faf7b492d45da1cb8db99186e176517f3284e3910b`.
Final frozen source, sibling, binary and runner identities passed before releasing
the checkpoint. Raw TRXs and identity files were unchanged. Three PowerShell
postprocessing corrections were necessary: cast the JSON-decoded DateTime directly
to DateTimeOffset instead of locale-dependent string parsing; sum OrderedDictionary
values by explicit keys; and group test IDs by explicit keys rather than a missing
adapted property. The last defect falsely grouped every assembly into one duplicate
identity. An independent actual-TRX comparison confirmed the cause.

The original failed reconciliation remains retained. A separate
`reconciliation-identity-corrected.json` and
`reconciliation-final-verification.json` record corrected accounting and unchanged
evidence under `.artifacts/concurrent-mutations/validation/p02bc-stable-qualified`.
The amendment runs no tests and does not renew a source/binary baseline. Its
elapsed time includes diagnosis and postprocessing, not only test execution.

Focused repair execution, full portability enforcement and final source/proof
review remain required. Later runtime, Workbench, maintenance and HTTP-schema
changes are not part of this frozen checkpoint's evidence.
