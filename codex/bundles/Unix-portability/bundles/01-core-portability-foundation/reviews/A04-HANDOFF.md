# A04 conditional-stop handoff

## Current state

- A04 implementation and all obtainable Windows/Linux/headless evidence are complete, including the reopened SEC-014 platform-baseline correction.
- Independent review and bounded remediation re-review are recorded in `reviews/14-a04-independent-review.md`.
- SEC-008, SEC-011, and SEC-013 remediation findings are independently closed.
- SEC-014 is independently GO: Windows `Auto` uses DPAPI/`Strong`; Unix `Auto` uses `LocalUserFile`/`BasicLocal` with `0700`/`0600` and a same-user warning.
- Gate C2 is GO after the operator explicitly deferred genuine macOS Keychain execution to `MACOS-KEYCHAIN-VALIDATION-001`.
- A05 is eligible. The runtime bundle remains dependent on later core gates.

## Review entry points

1. `reviews/13-a04-evidence-report.md`
2. `reviews/14-a04-independent-review.md`
3. `architecture/04-secrets-and-key-bootstrap.md`
4. `artifacts/unix-portability/A04/A04-static-audit-final.md`
5. `artifacts/unix-portability/A04/A04-secret-scan-final.json`
6. `artifacts/unix-portability/A04/A04-secret-scan-classification.md`
7. CodeAnalytics snapshot `snap-20260809191620-b07bdd50`
8. `artifacts/unix-portability/A04/remediation-2/`

The remediation-2 proof includes Windows 5,529/5,529 full Unit, both-host 99/99 focus, Windows 4/4 integration, Linux 3/3 plus 1/1 integration, both-host zero-warning Web builds, both-host HTTP 200 startup, and a schema-3 scan of all 36 source evidence files with no oversized/non-text/unreadable gaps and zero private-sentinel matches.

## Deferred macOS continuation

Use a genuine macOS interactive user session or an isolated macOS CI user with an available login Keychain. Docker, Linux virtualization, and injected-native tests do not satisfy this condition.

Run the existing actual-provider integration with `CANDOITALL_KEYCHAIN_INTEGRATION=1` and retain a TRX for:

```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'FullyQualifiedName~MacOs_keychain_actual_session_crud_and_restart_when_enabled' --logger 'trx;LogFileName=A04-macos-keychain-actual.trx'
```

The final macOS proof must cover probe/access-control state, create/read/update/delete, restart, concurrency, locked or interaction-denied behavior, and non-disclosure. Do not lock or replace a developer's normal login Keychain to manufacture evidence; use an isolated disposable user/keychain or an independently approved safe test arrangement. Refresh the macOS TRX, artifact scan, evidence report, independent C2 decision, canonical statuses, index/checksums, and checksum-enforcing validator afterward.

## Preserved residuals

- Linux production requires an operator-provided D-Bus session, Secret Service, and unlocked keyring.
- External wrapping-key and PFX confidentiality, backup, and rotation remain deployment responsibilities.
- Existing Infrastructure intra-project cycles and complexity hotspots remain later cleanup inputs.

## Next action

Enter A05. Run the deferred Keychain validation later on genuine macOS before changing the provider from actual-host unverified to verified.
