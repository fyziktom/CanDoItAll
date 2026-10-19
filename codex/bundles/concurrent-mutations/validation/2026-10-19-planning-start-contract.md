# Atomic planning start and original provider contract

The repaired candidate passes 123/123 selected tests. It does not enable product planning or close the
bundle. The design is in [product planning admission](../plan/P04-product-planning-admission.md).

Core now admits the exact displayed decision under the existing cross-instance dispatch
lease. One atomic file publication claims that lease, moves the run from Preparing to
Running and opens its first work interval. The original run, journal, source, authority,
chat and decision are checked again inside that publication. Current read access is
checked before acquiring the lease. No provider or SQL work occurs inside the callback.
Publication acknowledgement latency counts as active work. A lost acknowledgement
preserves the open interval; a restarted owner cannot substitute a new one.

The MAF port requires an originally captured contract digest. It combines the existing
provider configuration fingerprint with the Agent identity, resolved model/options and
participant prompt/schema. It checks the digest before provider construction and again
at actual transport admission. Administrative display text does not alter the contract.
Core carries the opaque typed digest and does not depend on MAF or module serialization.
Product admission still needs to retain that digest in original run metadata and route
explicit planning requests through the coordinator.

Six new file-owner cases exercise atomic revision changes and acknowledgement time,
two independent owners racing the same expectation, stale expectations, denied reads,
lost acknowledgements and cancellation while waiting for the real dispatch lease.
Nine adapter cases exercise provider/model/options/prompt/schema drift, a missing digest,
drift during provider construction, and equivalent configuration/display metadata.
The selected prior live-owner, saved-action, provider, admission, publication and receipt
tests protect the modified shared checks. Standard Release builds, confirmed discovery,
dependency review, full portability enforcement, observed execution and final identity
and resource reconciliation are required before this candidate is recorded as passing.

The previous blocked abandoned fixture and profiler directory remain explicitly tracked
in [resources](../resources.md). SPA remains on its separate branch without a merge.

The initial candidate `c4-planning-start-contract-validation` stopped at Integration
compilation: three new test assignments used a record update on constructor-validated,
read-only prompt properties. The tests now construct the replacement prompt through its
real validating constructor. Production builds, the Unit build and portability enforcement
passed; no test execution or freeze occurred for that rejected candidate. The replacement
candidate repeats the required checks against the corrected test source.

## Passing candidate

`c4-planning-start-contract-repair` passed 25 Unit and 98 Integration cases. Discovery
confirmed those exact counts. Core, MAF, Workbench and both test projects built in
standard Release. Full portability enforcement passed with 15,610 unchanged findings;
rendering, documentation and diff checks passed. Architecture snapshot
`snap-20261019082533-298e36b0` records three projects, one-directional project references
and four existing internal type cycles. Its 189 WS0002 package-advisory diagnostics
remain existing solution-load findings; this is not an advisory-free result.

Source identity `0E5F177976EFF7C9C8D992CEC2E301B1DD71F7A97F315DC11C3B3447AC07F237`
(9,227 files) and runtime identity
`466E703B1E629080DD714DB2BC5B7759B3F9A2FE468A2E8FEC1EF15FB3EB1E6B`
(6,436 files) remained unchanged. The observer covered the complete 3.662-second Unit
and 416.497-second Integration commands. A read-only stack sample during buffered test
output showed active template/catalog JSON loading; the run completed without a timeout.
Reconciliation found no remaining observed process or additional database. Six unowned
TEMP entries were preserved. Prior blocked cleanup resources remain in the baseline.
The [retained proof](../proof/2026-10-19-planning-authority/c4-planning-start-contract-repair.json)
contains all selected test results, command records and identity/resource/dependency
evidence. The freeze is released for product execution-entry integration.
