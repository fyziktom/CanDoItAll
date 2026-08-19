# B03 primary Gate R2 review

## Decision

`GO recommended; independent review pending.`

B03 satisfies MGR-001 through MGR-007 on the frozen source and focused evidence set. B04 remains blocked until the independent reviewer records Gate R2 GO and the bundle index/checksum records are regenerated.

## Architecture review

- Manager consumes the B01 process primitive through one owner-specific coordinator; it does not move Manager supervision semantics into MAF or create a broad platform service.
- The durable registry is the primary ownership authority. Platform discovery supplies bounded verification evidence only.
- Windows, Linux, and macOS discovery remain separate leaf strategies behind one typed Manager contract. WMI cannot leak into neutral Manager code.
- Watch, Tailwind, tuning, output pumping, durable recovery, and path monitoring retain narrow responsibilities. No new project or trivial interface layer was introduced.
- Dependency direction is outer Manager application to inner Core. The local 105-project/632-edge audit reports zero cycles and no reverse edge.

## Correctness and security review

- Automatic termination requires exact registry-first evidence. PID reuse, executable/owner/command mismatch, missing evidence, ambiguous duplicate leases, and permission denial remain fail-closed.
- Parent identity must match the current Manager before a launched child is registered. Recovery does not compare the historical PPID because Unix reparents surviving children; exact durable start/executable/command/owner identity remains mandatory and B01 revalidates exact start/executable identity at termination.
- Registry persistence stores fingerprints and typed identity, not raw argv, environment values, or secrets; restart non-disclosure is asserted by tests and the artifact scan.
- Linux `/proc`, macOS `libproc`, and macOS `ps` parsing is bounded. Malformed, oversized, missing, raced, non-rooted, interrupted, permission-denied, or locale-dependent evidence is rejected.
- Graceful shutdown is opt-in for live Manager sessions and always bounded by force-tree termination. Existing B01 behavior is unchanged for other callers.
- Filesystem comparison follows the physical host policy, preserving case-distinct Unix paths.
- Tuning substitutes typed values after tokenization; filesystem text cannot become new command syntax.

## Evidence integrity

- Windows/Linux focused unit/lifecycle slices: 139/139 on each host.
- Windows/Linux `ManagerPortability` integration: 11/11 on each host, including actual Linux parent-exit/recovery.
- Manager, Unit, and Integration affected builds: zero warnings/errors.
- Manager startup smoke: HTTP 200 with both optional supervisors stopped.
- Governed proof: 27/27 source and 11/11 artifact hashes match; ten assertions and eleven failing-first/characterization records are present.
- Source manifest: 62/62 unique IDs/paths, zero missing.
- Schema-3 scan: 13 candidates, 12 scanned text, one control output, zero coverage gaps/findings.
- `git diff --check`: clean except three already-recorded bundle CSV line-ending notices.

## Validation proportionality

The validation is deliberately focused: the exact B03 unit/lifecycle and categorized integration slices ran on Windows and pinned Linux, supplemented by affected-project builds and one Manager startup smoke. No broad solution suite was rerun, matching the operator's fast validation policy.

## Residuals

- Actual macOS is explicitly operator-deferred; deterministic fixtures remain mandatory and green.
- Windows sandbox WMI denial proves fail-closed behavior but not deployment availability.
- Hosted and final R4 evidence remain deferred.

## Handoff

Request independent Gate R2 review against reviews 14–15 and the governed artifact. Do not begin B04 until that decision is GO and integrity bookkeeping is complete.
