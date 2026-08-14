# B03 independent Gate R2 review

## Decision

`GO for implementation under RUNTIME-MACOS-VALIDATION-001.`

No blocking product, architecture, security, or evidence-integrity finding remains for MGR-001 through MGR-007 at the operator-approved local tier. This decision does not claim actual macOS validation, hosted validation, a broad/full-suite rerun, or final Gate R4.

## Independent findings

No blocking finding.

The prior Unix recovery defect is closed. `ManagerProcessCoordinator` still rejects a newly launched child unless its observed parent is the current Manager before registration. `ManagerProcessOwnershipVerifier` intentionally does not compare the historical parent during restart recovery, so a surviving Unix child can be reparented without losing its otherwise exact ownership record. Recovery still requires the registered PID, recovery start identity, filesystem-aware executable identity, observed-command fingerprint, and owner identity. The B01 host then independently requires exact UTC start-time equality and the executable fingerprint before termination. The deterministic reparenting regression and the actual Linux parent-exit/recovery test exercise both halves of that contract.

One non-blocking evidence wording correction remains: review 14 describes the persisted workspace value as a `workspace fingerprint`. The implementation intentionally persists the physical workspace root, as MGR-001 requires, while hashing planned argv and observed command evidence. Post-review bookkeeping should replace that phrase; it does not change the registry non-secret result because the asserted exclusion is raw argv, environment values, and secret text rather than required host-bound identity fields.

## Architecture and boundary result

- Manager remains the owner of durable process registration, typed purpose/lease policy, recovery decisions, Watch/Tailwind/tuning supervision, and diagnostics. B01 remains the single low-level process/session and exact termination implementation.
- Manager production sources contain no `Process.Start`, `new Process`, or `GetProcessesByName` call. `System.Management` and `ManagementObjectSearcher` occur only in `WindowsManagerProcessDiscovery.cs`.
- Composition registers one singleton `LocalWorkspaceProcessHost` and aliases the long-running host to that same instance. Recovery is registered before the Watch and Tailwind hosted services.
- The independently recomputed project graph contains 105 projects, 632 in-repository project-reference edges, zero cycles, and no Core-to-Manager reference. The dependency direction is outer Manager executable to inner Foundation/Core.
- Watch, Tailwind, and tuning consume the Manager coordinator without moving their domain policies into Core or merging their lifecycle owners.

## Correctness and security result

- MGR-001/MGR-005: the durable registry is the primary authority, uses bounded schema/count/file validation and private durable writes, and persists hashes instead of raw argv/environment data. Incomplete, permission-denied, mismatched, ambiguous, or foreign identity remains non-authoritative. Launch working directories are constrained to the authorized workspace root.
- MGR-002/MGR-003/MGR-004: platform discovery is leaf-specific. Linux uses bounded `/proc` reads. macOS combines kernel start/owner/parent identity with bounded invariant `ps` command evidence and now keeps generic nonzero, timeout, cancellation, start, termination, permission, malformed, and raced results fail-closed. Only authoritative absence is classified as exited. Windows WMI denial also remains a typed non-authoritative state.
- MGR-006: live Manager sessions opt into graceful-then-force-tree termination; recovered sessions use exact B01 identity termination. Already-cancelled host shutdown tokens no longer skip the bounded Watch/Tailwind reconciliation phases, lease completion is retryable after transient persistence failure, and DotnetWatch plus both Tailwind purposes are covered.
- MGR-006 watcher convergence: the Tailwind fingerprint advances only after exit code zero and confirmed output publication. The failed-then-successful same-fingerprint integration proves retry without another source change.
- MGR-007: restore traversal, project/reference collections, watcher filters, capsule comparisons, and recovery executable comparison use detected physical-filesystem semantics. Windows case-variant root and deterministic sensitive/insensitive executable fixtures close the previously divergent comparer paths.
- Tuning tokenizes the configured template before substituting typed path values, so a valid Unix quote in a filename cannot restructure argv.

## Evidence reconciliation

- Governed proof: 11 failing-first/correction records and 10 semantic assertions are present. I independently recomputed all 27 source hashes and all 11 test/build/host artifact hashes: zero missing files or mismatches.
- Test evidence: Windows and Linux unit/lifecycle TRXs each report 139/139; Windows and Linux `ManagerPortability` TRXs each report 11/11. The Linux TRX contains the actual parent-exit/recovery test; the Windows copy safely no-ops its Linux-only body.
- Builds/startup: the three affected build logs end with zero warnings/errors; the Manager startup log reports a loopback listener and successful application start with empty stderr.
- Source-reference manifest: 62 records, 62 unique IDs, 62 unique portable paths, zero missing paths.
- Redaction: the schema-3 scan accounts for 13 candidates as 12 scanned text artifacts plus one control output, with zero oversized/non-text/unreadable gaps and zero findings.
- Static checks: the governed anti-stub/source assertions reconcile with the current source. `git diff --check` reports only the three documented traceability-CSV line-ending notices.
- Portable validation: `python scripts/validate_bundle.py --bundle-root . --bundle runtime --stage portable` independently passed with 323 files, zero errors, and zero warnings before this review file was added.

No broad/full suite or broad build was rerun during this independent pass.

## Residual risks and follow-up

- Genuine macOS execution remains deferred. Deterministic `libproc`/`ps`, permission, locale, race, and filesystem-comparer fixtures are not actual-host proof; any later macOS failure reopens B03 and downstream gates.
- Windows WMI was permission-denied in the current sandbox. The safe denial path and deterministic mapper are proven, but deployment-policy availability is not.
- The macOS command parser deliberately fails closed for unsupported or ambiguous command shapes; those cases can require manual cleanup rather than automatic recovery.
- Hosted validation, a new broad/full suite, and final R4 remain deferred.

After correcting the review-14 wording, the executor must update the canonical R2/status records, regenerate the bundle index and checksums, and rerun the final validator. B04 becomes eligible only after that integrity bookkeeping; this review does not itself advance B04.
