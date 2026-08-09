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

## Final A04 evidence

- Windows: 5,524/5,524 full Unit, 94/94 focused security Unit, 4/4 secret/migration integration, and Web Release build process exit 0 with no warning/error hits.
- Linux Docker: 94/94 focused security Unit, 4/4 portable secret/migration integration, 1/1 actual D-Bus/GNOME Keyring Secret Service, and Web Release build process exit 0 with no warning/error hits.
- Headless: the external wrapping-key profile is restart-tested on Windows and Linux; missing keys fail closed.
- macOS: the Security.framework adapter has injected-native contract coverage, but genuine host execution is unavailable; SEC-002/A04-T11 makes this a Gate C2 blocker.
- Rollback: source verification precedes database publication; post-save interruption, retry, preservation, cleanup, and redacted failure audit regressions pass, including actual Windows DPAPI integration.
- Redaction: ten retained artifacts scanned; 24 synthetic TRX fixture matches across six fingerprints were reviewed. Schema 2 stores metadata/fingerprints only, loaded four private sentinels, and reported zero sentinel findings.
- Architecture: CodeAnalytics snapshot `snap-20260809191620-b07bdd50` has zero Error findings/diagnostics and no new project-reference edge.
