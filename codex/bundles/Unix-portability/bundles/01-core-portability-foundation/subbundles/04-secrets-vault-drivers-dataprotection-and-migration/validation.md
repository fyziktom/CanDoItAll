# A04 validation

## Focused commands

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Secret|FullyQualifiedName~Vault|FullyQualifiedName~DataProtection|FullyQualifiedName~DatabaseProfile'
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=SecretPortability|Category=SecretMigration'
```
```text
python ./scripts/scan_artifacts_for_secrets.py --root <repo>/artifacts/unix-portability/A04 --sentinel-file <private-test-input>
```

## Required proof

- Failing-first or characterization result.
- Focused unit/integration/actual-host result.
- Stable Windows regression result.
- Linux/macOS result when the subbundle changes platform behavior.
- Migration/rollback/failure-injection result where applicable.
- Redaction scan.
- Source/reference/requirement update.
- Independent review required by the active gate.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.

## Final A04 evidence before SEC-014 reopen

- Windows: 5,524/5,524 full Unit, 94/94 focused security Unit, 4/4 secret/migration integration, and Web Release build process exit 0 with no warning/error hits.
- Linux Docker: 94/94 focused security Unit, 4/4 portable secret/migration integration, 1/1 actual D-Bus/GNOME Keyring Secret Service, and Web Release build process exit 0 with no warning/error hits.
- Headless: the external wrapping-key profile is restart-tested on Windows and Linux; missing keys fail closed.
- macOS: the Security.framework adapter has injected-native contract coverage, but genuine host execution is unavailable; SEC-002/A04-T11 makes this a Gate C2 blocker.
- Rollback: source verification precedes database publication; post-save interruption, retry, preservation, cleanup, and redacted failure audit regressions pass, including actual Windows DPAPI integration.
- Redaction: ten retained artifacts scanned; 24 synthetic TRX fixture matches across six fingerprints were reviewed. Schema 2 stores metadata/fingerprints only, loaded four private sentinels, and reported zero sentinel findings.
- Architecture: CodeAnalytics snapshot `snap-20260809191620-b07bdd50` has zero Error findings/diagnostics and no new project-reference edge.

## SEC-014 remediation-2 evidence

- Windows: the exact reported `dotnet run` command returned HTTP 200 under Development `Auto`; the provider-selection regression proves current-user DPAPI with `Strong` protection and rejects explicit `LocalUserFile`; 99/99 focused security/database Unit and 5,529/5,529 full Unit tests passed; Web Release build completed with zero warnings/errors.
- Linux Docker without Secret Service, D-Bus, or an external wrapping key: Auto selected `LocalUserFile`, logged `BasicLocal` plus the same-user warning, and returned HTTP 200; 99/99 focused security/database Unit tests passed; Web Release build completed with zero warnings/errors.
- Cross-platform local store: restart continuity reads both legacy and new payloads, plaintext sentinels are absent, and Linux proves directory `0700` plus file `0600` modes.
- Provider policy: explicit DPAPI, Keychain, Secret Service, and external-key choices remain available and fail closed through their existing probes; explicit legacy `DataProtectionFile` remains Development/opt-in only and production-rejected.
- Non-disclosure: the schema-3 remediation scan covered all 36 retained source evidence files, loaded two private sentinels, found zero sentinel matches, reported zero oversized/non-text/unreadable source exclusions, and classified 72 occurrences of six existing synthetic fixture fingerprints confined to Unit TRXs. The scan also exposed and drove correction of an InMemory database-fingerprint connection-value leak; authoritative startup logs contain no connection password.
- Independent review: the second bounded SEC-014 re-review is GO after independent provider/taxonomy 5/5, scanner 3/3, evidence parsing, and portable validator 290/0/0 checks. The operator explicitly deferred genuine macOS Keychain execution to `MACOS-KEYCHAIN-VALIDATION-001`; C2 is GO and support remains actual-host unverified until that follow-up passes.
- Evidence root: `artifacts/unix-portability/A04/remediation-2/`.
