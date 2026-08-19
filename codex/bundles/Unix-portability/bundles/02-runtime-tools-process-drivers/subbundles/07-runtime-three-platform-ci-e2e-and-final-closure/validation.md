# B07 validation

## Focused commands

```text
./tools/Validation/Test-RuntimePortability.ps1 -Configuration Release -ResultsDirectory ./artifacts/unix-portability/B07/windows -UseLocalCanDoItAllLibraries $true
```
```text
./tools/Validation/Test-RuntimePortability.ps1 -Configuration Release -ResultsDirectory ./artifacts/unix-portability/B07/windows -UseLocalCanDoItAllLibraries $true -Scope Unit
```
```text
./tools/Validation/Test-RuntimePortability.ps1 -Configuration Release -ResultsDirectory ./artifacts/unix-portability/B07/windows -UseLocalCanDoItAllLibraries $true -Scope Integration
```
```text
./tools/Validation/Test-RuntimePortability.ps1 -Configuration Release -ResultsDirectory ./artifacts/unix-portability/B07/windows -UseLocalCanDoItAllLibraries $true -Scope Browser
```
```text
python ./scripts/validate_bundle.py --bundle-root . --repo-root <repo> --stage completed --bundle runtime
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

The runner always uses `--no-build --no-restore`, validates the exact governed class set and expected case totals, and supports `Unit`, `Integration`, `Browser`, or `All`. Build once after source edits; rerun only the affected scope while iterating, then run `All` once for the final host artifact.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.
