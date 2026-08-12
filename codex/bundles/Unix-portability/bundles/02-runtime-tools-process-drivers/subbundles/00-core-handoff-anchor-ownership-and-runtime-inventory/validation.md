# B00 validation

## Focused commands

```text
python ./scripts/scan_portability.py --repo-root <repo> --output <repo>/artifacts/unix-portability/B00/runtime-scan.json
```
```text
dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1
```
```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-build -p:UseLocalCanDoItAllLibraries=false --filter 'FullyQualifiedName~LocalWorkspaceProcessHostTests|FullyQualifiedName~WorkspaceCommandExecutionServiceTests|FullyQualifiedName~McpRuntimeContractsTests|FullyQualifiedName~ProjectStructureRuntimeLauncherTests|FullyQualifiedName~ProjectStructureRuntimeLauncherPathResolverTests|FullyQualifiedName~WorkspaceRuntimeProcessToolsTests|FullyQualifiedName~ProcessDriverAbstractionTests|FullyQualifiedName~BundledPluginWorkflowExecutorTests|FullyQualifiedName~TuningRequestServiceTests|FullyQualifiedName~TailwindSourceMonitoringTests'
```

The B00 slice is deliberately class-named rather than substring-broad. Run the same slice on Windows and Linux. Build the affected test project only when the existing Release output is stale; reserve a full solution run for the final runtime gate candidate or a change whose dependency reach cannot be bounded.

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
