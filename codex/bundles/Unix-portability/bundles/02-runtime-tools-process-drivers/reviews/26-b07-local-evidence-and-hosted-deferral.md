# B07 local evidence and hosted R4 deferral

## Decision

B07 implementation and the merge-readiness follow-up are locally green on Windows and Linux through C2. Final Gate R4 remains deferred because the configured hosted Windows/Ubuntu/macOS matrix has not executed against this candidate and genuine macOS arm64 colleague evidence is still pending. No macOS or hosted support claim is made. The current checkout is commit `386d8beb6038035f89a9a6961ec017d8213879a5` plus reviewed M00-M07 working-tree changes; M08 will freeze its immutable fingerprint.

## Fast validation contract

`tools/Validation/Test-RuntimePortability.ps1` is the single developer and CI entry point. `-BuildOnly` performs the one solution build and writes a durable commit/source/dependency/SDK/anchor/assembly stamp. `-SkipBuild` verifies that stamp before invoking prebuilt assemblies with `--no-build --no-restore`. The versioned `RuntimePortabilityCatalog.json` supports `Unit`, `Integration`, `Browser`, or `All` and rejects duplicate entries, missing FQNs/classes, zero tests, and count drift.

Expected totals:

- unit: 12 governed classes, 422 cases;
- integration: 7 governed classes, 45 cases;
- browser: one `AppSmokeTests` method, 1 case;
- full host gate: 468 cases.

The active `.github/workflows/ci.yml` matrix invokes this runner on `windows-latest`, `ubuntu-24.04`, and `macos-15`, installs Chromium first, and uploads TRXs, headless-host evidence, and B07 browser screenshots. That configuration is not evidence that the hosted jobs have executed.

## Historical B07 local evidence

The retained table below describes the original B07 snapshot and remains historical evidence; its 33-case integration artifacts are not relabeled. The current C2 follow-up proof is 422 Unit, 45 Integration, and one Browser case on both Windows and Linux, with the Linux browser executed in the exact Playwright 1.55 image plus .NET SDK 10.0.302.

| Host | Slice | Result | Artifact |
|---|---|---:|---|
| Windows 11 Pro 10.0.26200 x64; .NET SDK 10.0.303 | Governed unit | 422/422 | `artifacts/unix-portability/B07/windows/runtime-portability-unit.trx` |
| Same Windows host | Governed integration | 33/33 | `artifacts/unix-portability/B07/windows/runtime-portability-integration.trx` |
| Same Windows host; Chromium | Workbench runtime-node Playwright | 1/1 | `artifacts/unix-portability/B07/windows/runtime-portability-browser.trx` |
| Ubuntu 24.04 .NET 10 SDK container; image `sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0` | Same governed unit category | 422/422 | `artifacts/unix-portability/B07/linux/runtime-portability-unit.trx` |
| Same Ubuntu container | Same governed integration category | 33/33 | `artifacts/unix-portability/B07/linux/runtime-portability-integration.trx` |

The final Release solution build completed with zero warnings and zero errors. The affected Workbench component regression passed 9/9. The browser proof covers direct execution, explicit-script approval, missing executable dependency, platform-aware terminal/admin actions, headless terminal unavailability when applicable, foreign path syntax, and physical-path non-disclosure.

The final CI-contract and Process-adapter composition check passed 24/24. The source-reference manifest reconciles 177 records, 177 unique IDs, 177 unique paths, and zero missing paths, including six B07 records. `git diff --check` exits zero with four recorded line-ending notices. The portable runtime-bundle validator covers 341 files with zero errors and zero warnings.

`artifacts/unix-portability/B07/b07-governed-proof.json` validates and hashes all five retained final TRXs plus 18 B07 source/contract files. `artifacts/unix-portability/B07/b07-secret-scan.json` scans the five TRXs and governed proof with zero oversized, excluded, unreadable, or secret-bearing findings.

## Support and limitation matrix

| Profile | Local status | Hosted status | Supported claim |
|---|---|---|---|
| Windows desktop/headless | Current C2 468/468 focused gate green | Workflow configured, not yet executed for this snapshot | Local implementation proven; final hosted claim deferred |
| Ubuntu headless | Current C2 468/468 focused gate green including actual Chromium | Workflow configured with Chromium, not yet executed | Local implementation proven; final hosted claim deferred |
| macOS 15 | Deterministic lower-gate fixtures only | Workflow configured with Chromium, not yet executed | Unproven and unavailable for Final R4 claim |

Desktop open, interactive terminal, elevation, Docker, MCP, external tool, and Process capability availability remain profile- and dependency-sensitive. An unavailable capability returns the typed diagnostic established by B01–B06; it is never replaced by a shell or permissive fallback.

## Corrections made during B07

- Added a focused category and one exact no-build runner instead of repeating broad solution tests during iteration.
- Removed eager optional image-provider loading from Project Structure page initialization and eliminated the invalid-party lifecycle render wait.
- Corrected a scoped DI cycle by projecting the Standard Process adapter's static typed descriptor into host-capability composition rather than constructing execution drivers during capability probing.
- Added the Workbench runtime-node browser proof through the ordinary portfolio navigation path and made dialog transitions deterministic.
- Kept ordinary stable CI and the portability category disjoint so the focused slice is not executed twice.

## Rollback

Rollback is bounded to the `UnixRuntimePortability` traits, `Test-RuntimePortability.ps1`, the focused CI steps/artifact paths, and the B07 Workbench browser/lifecycle corrections. Do not roll back the B01–B06 ownership, capability, or receipt contracts to remove this gate.

## Deferred closure

Final Gate R4 requires the frozen M08 candidate, retained genuine macOS colleague artifacts (and hosted artifacts if the operator chooses to run that workflow), source/artifact reconciliation for that exact snapshot, and independent architecture/security/runtime/QA/operations review. Until then, RCI-001–005 and RCI-007 remain in progress; RCI-006 is locally solved.

The bundle's `completed` validation stage is intentionally not satisfiable at this handoff because it requires every requirement and Final R4 to be complete. The portable payload stage is green; no validator rule was weakened to hide the deferred hosted boundary.
