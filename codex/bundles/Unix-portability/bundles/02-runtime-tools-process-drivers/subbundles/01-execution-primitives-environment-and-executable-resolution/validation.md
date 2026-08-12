# B01 validation

## Focused commands

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~WorkspaceExecutableLocatorTests|FullyQualifiedName~WorkspaceCommandEnvironmentPolicyTests|FullyQualifiedName~LocalWorkspaceProcessHostTests|FullyQualifiedName~WorkspaceCommandReceiptWriterTests|FullyQualifiedName~WorkspaceExternalProcessRunnerTests|FullyQualifiedName~WorkspaceGitCommandExecutorTests|FullyQualifiedName~ToolImplementationContractsTests|FullyQualifiedName~RuntimeHostPlatformCapabilityTests.Architecture_limits_host_os_branches_to_reviewed_owners|FullyQualifiedName~WorkspaceCommandExecutionServiceTests'
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=ProcessPortability'
```

## Required proof

- Failing-first or characterization result.
- Focused unit/integration/actual-host result.
- Stable Windows regression result.
- Linux actual-host result when the subbundle changes platform behavior; deterministic macOS contract fixtures are mandatory and actual macOS is deferred under `RUNTIME-MACOS-VALIDATION-001`.
- Migration/rollback/failure-injection result where applicable.
- Redaction scan.
- Source/reference/requirement update.
- Independent review required by the active gate.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.
