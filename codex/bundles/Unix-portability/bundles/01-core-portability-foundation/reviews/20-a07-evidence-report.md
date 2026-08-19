# A07 implementation and local-readiness evidence report

## Decision requested

`LOCAL READINESS GO`. A07 implementation, current Windows/Ubuntu execution, static enforcement, headless restart, browser smoke, package fallback, and direct sibling-project development mode are locally ready for an exact-commit hosted candidate. Core Gate C4 remains `PENDING`: it requires the active workflow to pass on the pushed exact commit on Windows, Ubuntu, and genuine macOS, plus repository required-check evidence and an independent final decision.

The operator explicitly deferred genuine macOS Keychain CRUD proof as `MACOS-KEYCHAIN-VALIDATION-001`. That deferral does not substitute for the general macOS build, stable-test, portability, publish, and headless restart job required by C4.

## Implementation

- Restored `.github/workflows/ci.yml` as an active least-privilege workflow with Windows, Ubuntu 24.04, and macOS 15 stable jobs, a Linux static gate, and a disposable Compose gate. Each host also runs the PostgreSQL-backed migration/storage slice that the normal stable filter deliberately excludes.
- Forced package dependency mode in hosted CI with `UseLocalCanDoItAllLibraries=false`; local development continues to use the sibling Components and FileTools project references when both repositories are present.
- Added actual-host portability categories and a headless validator that publishes outside the checkout, starts twice against isolated roots, verifies health/readiness/database migrations/seven purpose roots, and captures redacted evidence.
- Added a Windows browser smoke and a short private work root so Windows path limits cannot invalidate an otherwise portable publish.
- Added deterministic portability scanning, complete classification, an executable-source baseline, scanner tests, documentation/installer/Docker gates, and an active-CI README badge.
- Corrected test fixtures that treated Windows temporary folders or platform separators as portable logical contracts. Production behavior was not weakened to accommodate those fixtures.

## Fast validation policy

Validation is layered to avoid repeatedly running every suite:

1. a named regression filter after an implementation edit;
2. the affected test project or active subbundle filter before review;
3. one stable solution run per actual host and gate candidate;
4. the complete Windows/Ubuntu/macOS matrix once on the exact hosted commit.

The authoritative 7,459-test Windows and Ubuntu results below remain reusable because subsequent edits are limited to documentation, evidence, static tooling, workflow coverage, its source contract, and the Windows headless validator's private work-root handling. The workflow/headless changes are covered by a current 2/2 regression, a current two-cycle Windows headless run, and a current 10/10 PostgreSQL-backed slice on both Windows and native Ubuntu. No solution-wide rerun is warranted by those changes.

## Validation

| Proof | Result | Durable evidence |
|---|---|---|
| Windows package-mode build | Release build succeeded with 0 warnings and 0 errors | `artifacts/unix-portability/A07/windows/final/A07-windows-package-build.log` |
| Windows direct sibling-project build | Clean restore/build compiled Components and FileTools source in Release with 0 warnings and 0 errors | `artifacts/unix-portability/A07/windows/final/A07-windows-direct-local-build-after-clean.log` |
| Windows stable suite | Components 958/958; Integration 720/720; MAF Memory 22/22; Memory 196/196; Unit 5,563/5,563; aggregate 7,459/7,459 | `artifacts/unix-portability/A07/windows/final/stable-final3/*.trx` |
| Ubuntu stable suite | Components 958/958; Integration 720/720; MAF Memory 22/22; Memory 196/196; Unit 5,563/5,563; aggregate 7,459/7,459 | `artifacts/unix-portability/A07/linux/stable-native-recheck/**/*.trx` |
| Current CI/headless regression | 2/2, including the short redacted Windows work-root contract and the required PostgreSQL-backed matrix slice | `artifacts/unix-portability/A07/windows/final/targeted/A07-windows-ci-workflow-regressions-postgres-final.trx` |
| PostgreSQL migration/storage slice | Windows 10/10 and native Ubuntu 10/10; storage-catalog old/new format, restart, rebind/rollback, bootstrap preservation, and managed-files integration use the already-provisioned PostgreSQL service | `artifacts/unix-portability/A07/windows/final/targeted/A07-windows-postgres-migration-restart-authoritative.trx`; `artifacts/unix-portability/A07/linux/stable-native-recheck/integration/A07-linux-postgres-migration-restart-authoritative.trx` |
| Fixture remediation | Components 2/2, Unit 49/49, Integration 1/1 after canonical logical-path corrections | `artifacts/unix-portability/A07/windows/final/targeted/*.trx` |
| Windows headless/browser | Two Healthy/Ready cycles, migrations ready, seven roots, outside-repository publish/state, browser smoke passed, no captured secret/root; forced process-tree stop recorded honestly | `artifacts/unix-portability/A07/windows/final/headless-final4/headless-summary.json`; `browser-smoke.trx` |
| Ubuntu headless | Two Healthy/Ready cycles, migrations ready, seven roots, outside-repository publish/state, SIGTERM exit 0, no captured secret/root | `artifacts/unix-portability/A07/linux/headless-final/run/headless-summary.json` |
| Documentation | 171 maintained Markdown files passed; all projects have README coverage and the active workflow badge is present | `artifacts/unix-portability/A07/windows/final/A07-windows-documentation-validation-final.log` |
| Install scripts | Windows installer harness passed parsing, WhatIf non-mutation, and generated-launcher checks | `artifacts/unix-portability/A07/windows/final/A07-windows-install-script-validation-final.log` |
| Docker conventions | Repository Docker validation and shared standards convention validation passed with 0 errors/warnings | `artifacts/unix-portability/A07/windows/final/A07-windows-docker-validation.log`; `A07-windows-shared-docker-conventions.log` |
| Architecture | Snapshot `snap-20260810211432-d225a84b` contains 3,377 findings: 713 Warning, 2,664 Info, and 0 Error findings | CodeAnalytics snapshot |
| Portability inventory | 4,825 files; 27,252 findings classified; 0 unclassified; expected workflow PostgreSQL-gate delta failed before explicit refresh; 12,952 reviewed executable-source findings then enforced unchanged | `artifacts/unix-portability/A07/static-guard-final4/*` |
| Artifact redaction | Schema 3; 230 candidates; 220 text files scanned; seven non-text files and three control inputs explicitly accounted; zero oversized/unreadable files; two sentinels and zero sentinel matches | `artifacts/unix-portability/A07/A07-secret-scan-final.json` |

## Secret-scan classification

The 96 generic findings are exactly six known synthetic test-fixture fingerprints, each occurring 16 times across eight retained Windows/Linux TRXs: one GitHub-shaped token, one OpenAI-shaped token, and four secret-assignment fixtures. The report contains only rule names and truncated fingerprints. There are no sentinel matches, coverage gaps, runtime startup-log findings, connection values, vault payloads, or unaccounted large text files. Private sentinel input files were deleted after the scan.

## Failure classification

- The initial Windows full run exposed test fixtures that used protected `%TEMP%` paths or expected physical Windows separators in canonical logical output. Focused failing-first results are retained; corrected focused tests and the authoritative full run are green.
- A later non-elevated Unit-only diagnostic run had 5,563 passes and one `%LOCALAPPDATA%` sandbox denial. The same test passed in the authoritative elevated full run; this is invalid sandbox evidence, not a product regression.
- The first Windows PostgreSQL slice had 2 passes and eight sandbox-denied Web-host storage tests. The same unchanged 10-test filter passed 10/10 with normal host permissions. The native Ubuntu slice also passed 10/10.
- A direct-local focused build attempted after package-mode outputs were present produced duplicate sibling-package assembly identities. Rebuilding only the Unit project in explicit package mode succeeded with zero warnings/errors. The already-proven clean direct-local build remains authoritative for sibling-source mode.
- The first Windows headless publish used a long evidence-derived intermediate path and hit the operating-system path limit. The validator now uses a short private work root while keeping sanitized evidence in the requested output. The focused regression and two-cycle rerun pass.
- Windows hidden-process execution has no managed console signal path, so the validator records `ProcessTermination` and exit `-1` instead of claiming graceful shutdown. Linux proves graceful SIGTERM. Windows service graceful-stop proof remains an operations residual.

## Hosted boundary

C4 is intentionally not issued from local evidence. The exact candidate still needs:

- a committed and pushed anchor authorized by the operator;
- successful `stable-windows-x64`, `stable-ubuntu-x64`, `stable-macos-arm64`, `portability-static`, and `containers` jobs for that commit;
- downloaded TRX/headless artifacts with macOS actual-host provenance and restart evidence;
- evidence that the required checks protect the merge boundary;
- final independent architecture/security/operations review and a completed `CORE-C4-HANDOFF.md`.

## Residuals

- Genuine macOS general host validation is pending hosted CI; no macOS actual-host support claim is made yet.
- Genuine macOS Keychain CRUD/restart/concurrency/access-control proof remains the explicitly non-blocking follow-up `MACOS-KEYCHAIN-VALIDATION-001`.
- Windows graceful service-stop behavior remains to be demonstrated in final operations evidence; the local headless validator is deliberately truthful about forced termination.
- The latest architecture snapshot has no Error finding. Its larger warning/info inventory reflects the architecture-wide current solution and direct sibling dependency surface; existing cycles remain later architectural inputs rather than new A07 boundary errors.
