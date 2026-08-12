# B05 primary Gate R3b review

## Decision

**GO after bounded remediation and independent re-review.** B05 satisfies PLUG-001 through PLUG-005 within the operator-approved local Windows/Linux boundary and explicit actual-macOS deferral. The five findings in review 22 are corrected and independently accepted. B06 is eligible after canonical checksum/index bookkeeping.

## Architecture review

- Docker owns Docker recipe and dependency-state meaning only. Physical process ownership, final executable resolution, environment selection, workspace scope, timeout, cancellation, bounded output, and tree termination remain in the B01 authorities.
- One scoped Docker instance implements both execution and capability-probe contracts. A scoped asynchronous runtime catalog and the execution wrapper consume that state without a synchronous descriptor block, process-global mutable authority, duplicate probe, or duplicate local process implementation.
- FileTools remains the only desktop OS-delegation adapter. Application code owns authorization, runtime-profile availability, host-bound preference selection, and trusted path policy without reproducing desktop-launch internals.
- No broad platform service was added. B05 adds no production project-reference edge and the 106-project/635-reference graph remains acyclic.

## Security and correctness review

- Docker configuration inheritance is a named allowlist; ambient credentials are excluded. Host environment keys use the selected host comparer.
- Foreign configuration paths, unsafe root/link traversal, invalid contexts/API versions, unsupported endpoint components, credentials, and named-pipe/socket ambiguity fail before execution.
- Only authoritative successful empty Docker inventories mean absence. Indeterminate container/image/running-state results fail before `start`, `pull`, or `run`.
- Docker output and messages redact protected endpoint/config/certificate/socket values plus host-normalized and separator-trimmed variants, local Unix socket payloads, and shared secret-shaped text.
- Desktop actions require feature enablement, an interactive profile, validated direct-source mode, package session availability, trusted local path authority, and a safe host-bound preferred executable where configured. Package-mode desktop integration fails closed until publication/re-pin.
- Rooted physical paths no longer lose significant Unix trailing spaces at the shared guard. Logical relative paths retain their existing canonical parser.
- FileTools cancellation is rechecked immediately before process delegation and tested with cancellation injected during preflight; no post-shell-delegation ownership is invented.

## Evidence review

- Parsed retained TRX totals remain green; refreshed remediation totals are Windows/Linux 38/38 unit, 20/20 FileTools, and 2/2 integration.
- Ten affected build logs contain zero warning/error hits, including refreshed Web, Docker plugin, and FileTools builds.
- Governed proof: 15 corrections, 16 assertions, 29/29 source hashes, 25/25 test/build/host artifact hashes, zero mismatch.
- Source references: 135 records, 135 unique IDs, 135 unique paths, zero missing.
- Schema-3 scan: 27 candidates, 26 text scanned plus one control output, zero coverage gaps, zero findings.
- `git diff --check` exits zero; the three existing CSV line-ending notices remain informational.

## Requirement disposition

| Requirement | Status | Review conclusion |
|---|---|---|
| PLUG-001 | Implemented | Canonical host dependencies are injected; duplicate owner is removed. |
| PLUG-002 | Implemented | Staged typed dependency states are consumed by the product; mutation preflights and endpoint authority fail closed. |
| PLUG-003 | Implemented with recorded identity boundary | Exact direct-source compatibility is proven; unverified package-mode desktop behavior is disabled pending publication/re-pin. |
| PLUG-004 | Implemented | Desktop behavior is optional, direct-source validated, host-bound, headless-safe, and cancellation-safe until OS delegation. |
| PLUG-005 | Implemented | Dependency ledger and probes are complete for B05 scope. |

## Deferred boundaries

Actual macOS is deferred by operator instruction. Hosted CI, the final broad aggregate, and R4 remain B07 work. The direct FileTools source must be published/re-pinned deliberately after this branch completes; this gate does not claim the corrected working tree is already NuGet `0.1.18`, and package-mode desktop launch is unavailable meanwhile.

## Independent review request

Bound the re-review to B05-IND-001..005 and refreshed consistency: production Docker availability consumption, fail-closed mutation sequences, endpoint/path/redaction authority, package-mode quarantine, final FileTools cancellation checkpoint, governed hashes, TRX/build counts, source references, and schema-3 coverage. Append only `## Re-review` to `reviews/22-b05-independent-r3b-review.md`.
