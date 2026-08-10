# A07 independent local-readiness review

Date: 2026-08-10

## Decision

**LOCAL READINESS GO.** I found no blocker to using the current A07 tree as the candidate for an exact-commit hosted run.

This is deliberately not Core Gate C4. `CI-001`, the macOS portions of `CI-002` through `CI-004`, and `CI-007` remain open until the exact pushed commit passes the protected hosted matrix and the C4 handoff is completed. Bundle B00 remains blocked.

## Findings

No local-readiness blocker remains.

The earlier test-selection gap is closed. `.github/workflows/ci.yml` now provisions PostgreSQL and positively schedules `(Category=UnixPortabilityCore)&(RequiresHostDocker=true)` for every stable matrix entry. This is separate from the intentional no-Docker stable and portability filters. The workflow contract is 2/2, and the unchanged positive-host filter passes 10/10 on Windows and 10/10 on actual Linux. The ten-test slice includes the PostgreSQL storage-catalog migration/restart/rollback and bootstrap-preservation cases as well as the managed-files storage cases.

The following are follow-ups, not reasons to reject local readiness:

- **C4 source-provenance condition:** the default direct-local graph currently also depends on sibling repository state. Independently observed sibling heads are Components `f5c477980316f4f7c8363945eb9624db1ab6e867` and FileTools `47ea01b61a174c435775504724e2922ae54769e5`; each has an uncommitted `Directory.Build.props` configuration-propagation edit. Before C4, either commit and record those exact sibling anchors/dirty-state resolutions in the handoff or remove their necessity. A CanDoItAll commit alone is not a complete provenance statement for the default direct-local graph.
- **Workflow supply-chain hardening:** `.github/workflows/ci.yml:48`, `:51`, `:57`, and `:149` use mutable major-version action tags. The workflow has only `contents: read` and supplies no production secret, so this does not block the local candidate. For a higher-integrity protected gate, pin at least the third-party PostgreSQL action, preferably all actions, to reviewed full commit SHAs or record the repository's accepted action-pinning policy.
- The Windows headless harness truthfully records forced process termination and exit `-1`; it does not prove graceful Windows service shutdown. Preserve that limitation in C4 operations evidence.

## Requirement disposition

| Requirement | Local disposition | Remaining closure |
|---|---|---|
| `CI-001` | Active matrix and commands are locally ready | Exact-commit Windows, Ubuntu, and genuine macOS jobs plus required-check/repository-policy evidence |
| `CI-002` | Windows and actual Linux evidence is adequate and host-sensitive; tests do not depend only on a mocked OS enum | Genuine macOS stable, portability, provider-selection, permissions, and headless execution |
| `CI-003` | Windows/Linux protected-state restart and the PostgreSQL storage/migration slice are scheduled and green | Genuine macOS restart/migration execution; Keychain CRUD remains the separately deferred item below |
| `CI-004` | Windows/Linux publish and two-cycle startup run outside the checkout and outside repository mutable roots | Genuine macOS outside-checkout publish/start/restart evidence |
| `CI-005` | Pass: authoritative Windows aggregate is 7,459/7,459, with the current affected slices green | Repeat on the exact hosted commit |
| `CI-006` | Pass locally: deterministic scan, complete classification, negative delta proof, and baseline enforcement are current | The hosted `portability-static` job must pass on the exact commit |
| `CI-007` | Not satisfied by design | Exact anchor, CI links/artifacts/checksums/support matrix, independent C4 decision, and completed `CORE-C4-HANDOFF.md` |

The operator-deferred `MACOS-KEYCHAIN-VALIDATION-001` remains non-blocking for this progression only while macOS Keychain support stays `ActualHostUnverified`. It does not waive the general genuine-macOS build, test, PostgreSQL migration/restart, publish, or headless evidence required for C4.

## Workflow, security, and dependency review

- The workflow triggers on main pushes, main pull requests, and manual dispatch; uses a fixed Windows/Ubuntu/macOS matrix; disables fail-fast; declares `contents: read`; and applies bounded timeouts and cancellation concurrency.
- Restore, build, stable tests, focused host tests, the PostgreSQL-backed slice, and headless publish all explicitly select package mode with `UseLocalCanDoItAllLibraries=false`. This avoids an accidental dependency on adjacent checkouts in hosted CI.
- Independent MSBuild evaluation confirms the two intended dependency modes. Explicit package mode retains the matching Components/FileTools packages and no sibling project references. Direct-local mode removes those package identities and substitutes the corresponding sibling projects. Clean package and clean direct-local Release build evidence is green; the recorded mixed-output duplicate-identity failure is correctly rejected as non-authoritative rather than hidden by a fallback.
- The headless validator publishes to a private work root outside the checkout, starts the product twice, verifies health/readiness/migrations and all seven purpose roots, and emits hashes and redacted logical state rather than physical roots or secrets. Windows shows two Healthy/Ready cycles and browser smoke; Linux shows two Healthy/Ready cycles with SIGTERM exit 0.
- The workflow-provided PostgreSQL password is a fixed disposable test credential, not a production credential. Artifact uploads are bounded and retained for 14 days. C4 must download and rescan the exact hosted artifacts before relying on them.

## Architecture gate

Status: **Pass with C4 follow-ups**.

- A07 adds build/test/workflow composition rather than a new product service boundary. I found no new service-locator mechanism, runtime registration drift, or partial-class ownership split in the A07 surface.
- `Directory.Build.targets` performs one conditional dependency substitution at the build boundary. It does not add package and project identities together, and configuration/platform propagation is explicit. Package-mode CI therefore remains standalone while direct-local development consumes source.
- CodeAnalytics snapshot `snap-20260810211432-d225a84b` is present, fresh, and not cache-derived, with 3,377 findings. The reported distribution is 713 Warning, 2,664 Info, and zero Error findings. An independent finding query for `Error` returned only five Info findings whose type names contain that word.
- The dependency query exposes existing module/type cycles in the wider solution/sibling scope but no project-level cycle. Clean builds in both conditional dependency modes provide the executable graph check. These wider cycles were not introduced by an A07 production boundary and remain architecture backlog, not a reason to claim the current solution is cycle-free.
- Testability is adequate for this gate: the active workflow contract guards matrix/filter/static/headless composition, while the behavior evidence exercises both dependency modes, actual-host selection, restart, failure classification, and redaction.

## Evidence reconciliation

- Authoritative stable results independently parse to 7,459/7,459 on Windows and 7,459/7,459 on Linux: Components 958, Integration 720, MAF Memory 22, Memory 196, and Unit 5,563. A zero-test Playwright TRX is not included in either aggregate.
- Current affected proof independently parses to workflow contract 2/2, Windows PostgreSQL 10/10, and Linux PostgreSQL 10/10, all with zero failures.
- Reuse of the two full stable runs is valid under the documented invalidation policy. The later change is confined to workflow selection and its contract; the selected product/integration behavior is covered by the fresh 10/10 runs on both available hosts. Re-running the full suites would not add relevant information.
- The final portability scan reports 4,856 candidates, 4,825 scanned text/source files, 31 accounted large/binary skips, 27,252 classified findings, and zero unclassified findings. The pre-refresh enforcement failed on exactly the intended new workflow fingerprint; the reviewed executable-source baseline was then refreshed to 12,952 and enforced unchanged.
- The schema-3 artifact scan accounts for all 230 candidates: 220 text files scanned, seven non-text files and three control inputs accounted, and zero oversized or unreadable text files. Two private sentinels were loaded with zero matches. The 96 generic findings are six known synthetic fingerprints, each occurring 16 times across eight retained Windows/Linux Unit TRXs; findings contain metadata and truncated fingerprints only.
- The portable bundle validator passed with frozen checksums before this review was added: 303 files, zero errors, zero warnings. `git diff --check` is clean apart from the already recorded traceability CSV EOL notice.

## Exact C4 boundary

Do not convert this decision into C4 GO. C4 still requires all of the following on one immutable candidate:

1. Commit and push the authorized CanDoItAll candidate; resolve and record the sibling-source provenance noted above.
2. Obtain successful `stable-windows-x64`, `stable-ubuntu-x64`, `stable-macos-arm64`, `portability-static`, and `containers` checks for that exact commit. The macOS stable job must include the newly positive PostgreSQL slice and the general actual-host/headless coverage.
3. Download the exact-run TRX and headless artifacts, verify host/OS/runtime/architecture provenance and two-cycle restart, and run the complete schema-3 redaction scan over those downloaded artifacts.
4. Record repository-policy evidence that the named checks protect the merge boundary.
5. Regenerate the bundle index/checksums after this review and all final bookkeeping, run the completed-stage validator, and perform the final independent architecture/security/operations review.
6. Complete `reviews/CORE-C4-HANDOFF.md` with exact repository and sibling anchors, CI links, artifact checksums, supported profiles, `ActualHostUnverified` limitations, and the source/contract delta B00 must revalidate.

Until those items pass, C4 remains pending and B00 remains blocked.
