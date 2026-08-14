# A06 implementation and evidence report

## Decision requested

`GO`. The initial independent review recorded `NO-GO` for three findings. All three were remediated, the affected Windows, Ubuntu, static, architecture, portability, and non-disclosure evidence was refreshed, and the bounded independent re-review in `reviews/19-a06-independent-review.md` recorded Gate C3b/Hosting GO. A07 is eligible.

## Support boundary

- `win-x64` and `linux-x64` framework-dependent artifacts have actual-host start, health, restart, shutdown, and durable host-provenance evidence.
- Linux evidence is bound to the official `mcr.microsoft.com/dotnet/aspnet:10.0-noble` Ubuntu 24.04 image by immutable image ID and digest. The application ran as UID/GID `10001:10001`.
- `osx-x64` and `osx-arm64` framework-dependent artifacts publish successfully, contain the same support manifest, and have statically validated launchd/install assets.
- macOS is deliberately labeled `ActualHostUnverified`. Genuine macOS headless execution is deferred to A07 as `A07-MACOS-HEADLESS-ACTUALHOST-001` and must not be inferred from cross-publish or XML validation.
- macOS Keychain execution remains the separate operator-approved follow-up `MACOS-KEYCHAIN-VALIDATION-001`.
- Windows headless `Auto` uses current-user DPAPI/`Strong`. Unix headless `Auto` uses permission-hardened `LocalUserFile`/`BasicLocal` and emits a non-sensitive limitation warning. Production Unix additionally requires certificate-backed ASP.NET Core Data Protection key-ring protection.

## Implementation

- Added a typed, embedded deployment-support manifest and strict codec in `src/App/CanDoItAll.Composition/RuntimeDeploymentSupport.cs` and `RuntimeDeploymentSupport.json`.
- Published the manifest as `runtime-support.json` and exposed a typed, redacted `/api/runtime/operations` projection.
- Added framework-dependent publish/support tests and host-profile capability tests.
- Added non-elevating Unix install, active-release launcher, and idempotent rollback scripts.
- Added hardened systemd and launchd templates plus a Linux/macOS headless operations runbook.
- Preserved the Windows installer and separated Unix service integration from application security/path policy.
- Connected the sibling Components and FileTools repositories through direct project references when both source trees exist. NuGet remains the fallback when the sibling repositories are absent. Configuration propagation is fixed in both sibling repositories so Release consumers cannot silently rebuild those projects as Debug.

## Initial-review remediation

| Initial finding | Remediation and proof |
|---|---|
| HOST-003 launchd system daemon lacked service identity | The LaunchDaemon template now requires `UserName` and `GroupName` tokens. The runbook scopes it to `/Library/LaunchDaemons`, requires a dedicated account and owned roots, and rejects unresolved tokens. Static XML/rendering and source tests verify both identity keys and no hard-coded root identity. |
| HOST-005 omitted configured-purpose-root diagnostics | Infrastructure owners now resolve, policy-check, and write-probe all seven typed roots independently. Operations exposes only purpose, configuration source, state, and reason. Windows and Linux captures each contain seven Ready facts and no physical path, environment value, host-binding ID, or exception text. |
| HOST-001 actual-host provenance was not durable | New Windows and Ubuntu provenance records bind the executed artifacts to OS/runtime/architecture, outside-repository hashes, effective identity, lifecycle commands/statuses, and readiness. The records explicitly exclude secrets and physical purpose roots. |

The native Linux compile also exposed a test-only scalability bug: the architecture inventory traversed generated `bin`, `obj`, and `artifacts` trees before filtering them. It now prunes those directories during traversal while preserving the source assertion. The final native Ubuntu focused suite passes.

## Validation

| Proof | Result | Durable evidence |
|---|---|---|
| Windows focused deployment/profile tests | 32/32 passed | `artifacts/unix-portability/A06/final/windows/A06-focused-final.trx` |
| Ubuntu SDK focused deployment/profile tests | 32/32 passed after native restore/build | `artifacts/unix-portability/A06/final/linux/A06-linux-focused-final.trx`; `A06-linux-focused-final.log` |
| Windows full Unit regression | 5,561/5,561 passed | `artifacts/unix-portability/A06/final/windows/A06-windows-full-unit-authoritative.trx` |
| Known narrative timing classification | The unrelated lease-reclaim test passed in the current isolated rerun and the final authoritative suite | `artifacts/unix-portability/A06/final/windows/A06-narrative-timeout-current-rerun.trx`; authoritative TRX |
| Main Release solution build | Clean cross-host rebuild, 0 warnings, 0 errors; direct Components/FileTools sources compiled in Release | `artifacts/unix-portability/A06/final/windows/A06-windows-solution-build.log` |
| Components and FileTools Release solution builds | Both 0 warnings, 0 errors | `artifacts/unix-portability/A06/final/windows/A06-components-solution-build.log`; `A06-filetools-solution-build.log` |
| Four-RID publish | win-x64, linux-x64, osx-x64, osx-arm64 complete; Web DLL, Templates, wwwroot, and manifest present; manifest hashes match; 68 Release local-library references and 0 Debug references | `artifacts/unix-portability/A06/final/publish/A06-publish-matrix.json` and per-RID logs |
| Windows actual host | Published output and disposable PostgreSQL: two Ready/HTTP-health starts, migrations ready, seven purpose roots, current-user non-System identity, redacted commands | `artifacts/unix-portability/A06/final/windows/A06-windows-host-provenance.json`, startup/health/operations captures |
| Ubuntu actual host | Official Ubuntu 24.04 .NET 10 image, unprivileged UID/GID 10001, fresh PostgreSQL/network/volumes, mode-0600 Data Protection certificate: v1 start, v2 upgrade, rollback to v1, three Ready snapshots and clean SIGINT exits | `artifacts/unix-portability/A06/final/linux/A06-linux-host-provenance.json`, startup/health/operations captures |
| Unix installer lifecycle | v1 install/launch, duplicate and relative-path rejection, v2 upgrade, rollback, and repeated rollback passed | `artifacts/unix-portability/A06/final/linux/A06-unix-installer-lifecycle.log` |
| Service assets | Alpine `sh -n`, LF-only scripts, systemd hardening/rendering, launchd XML/profile/identity and complete rendering passed | `artifacts/unix-portability/A06/final/static/A06-service-template-validation.json` |
| Architecture | Snapshot `snap-20260810044522-a1246e1e`: 3 projects, 1,733 types, 13,417 members, 135 registrations, 0 Error findings, 0 Error diagnostics | CodeAnalytics snapshot |
| Portability inventory | 4,840 candidates, 4,812 scanned, 28 large/binary accounted, 27,094 findings classified, 0 unclassified, non-truncated | `artifacts/unix-portability/A06/final/static/A06-portability-scan*.{json,csv,md}` |
| Artifact redaction | Schema 3, 52 candidates, 51 scanned plus one output control, 0 coverage gaps, 2 sentinels/0 matches; 24 findings are six synthetic fixture fingerprints contained only in the two retained full-suite TRXs | `artifacts/unix-portability/A06/final/static/A06-secret-scan.json` |
| Mechanical summary | Every final parsed TRX/build/publish/runtime/provenance/template/scan gate is green | `artifacts/unix-portability/A06/final/A06-validation-summary.json` |

## Synthetic-secret classification

The six unique fingerprints are generated test inputs, not credentials:

- approval-audit redaction cases containing an `api_key` fixture;
- workflow-observability JSON redaction fixtures;
- the dedicated secret-scanner OpenAI-shaped and GitHub-shaped rejection fixtures;
- a spoofed managed-envelope redaction fixture;
- an invalid endpoint containing an `api_key` fixture.

They appear twice because the initial and authoritative full-suite TRXs are both retained. The schema-3 report stores metadata and truncated fingerprints only. The Ubuntu provenance records no sensitive values or physical purpose roots; the Windows record likewise omits credential values, environment dumps, connection strings, roots, vault paths, certificates, and identity names.

## Portability-scan review

Every finding is routed by the deterministic classifier. A06-specific occurrences were manually reviewed:

- script shebang matches are syntax markers, not elevation; no Unix application script invokes `sudo`, `su`, or an automatic privilege path;
- `chmod`, `umask`, immutable release directories, and atomic state-file moves are intentional permission/durability controls;
- support-manifest provider names and deferral IDs are typed capability metadata, not secret material;
- the operations endpoint projects bounded state and redacted per-purpose facts and is covered by non-disclosure tests plus both-host captures;
- `Directory.Build.targets` matches the broad external-tool pattern only because it contains the requested sibling FileTools bridge. It preserves NuGet fallback and introduces no runtime tool discovery;
- the Linux validation script uses unique disposable resources, a service-owned certificate volume, explicit unprivileged identity, exact cleanup targets, and metadata-only provenance.

See `artifacts/unix-portability/A06/final/static/A06-changed-source-portability-review.md` for the explicit changed-source disposition.

## Residuals

- Genuine macOS execution is required in A07 before any actual-host-verified macOS support claim.
- Genuine macOS Keychain execution remains tracked separately for later execution on a real Mac.
- `systemd-analyze` was unavailable in the runtime image; the unit contract, shell syntax, hardening directives, rendered-token completeness, and actual Ubuntu launcher lifecycle were validated.
- Ubuntu startup retains optional GSSAPI loader noise and the HTTP-only test profile emits an HTTPS-redirection warning. Both are non-blocking for Ready/health/shutdown proof and remain operational cleanup before final C4 support closure.
- Existing test-helper type cycles reported by CodeAnalytics are unchanged and do not cross production project boundaries.
