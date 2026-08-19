# B04 validation

## Focused commands

```text
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --no-build --filter 'FullyQualifiedName~Mcp|FullyQualifiedName~ExternalProcess|FullyQualifiedName~ToolImplementationContractsTests|FullyQualifiedName~WorkspaceExternalProcessRunnerTests|FullyQualifiedName~WorkspaceExecutableLocatorTests|FullyQualifiedName~LocalWorkspaceProcessHostTests.Duplex_session|FullyQualifiedName~CapabilityFoundationHardeningTests|FullyQualifiedName~CapabilityMigrationCleanupGuardTests|FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~Expand_preserves_significant_whitespace_in_a_physical_path'
```
```text
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --no-build --filter 'Category=McpPortability|Category=ExternalToolPortability|Category=ProcessPortability'
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

Actual macOS execution is operator-deferred and must not block local gate progression. Deterministic host-profile coverage remains required; genuine macOS proof is retained for the final platform validation boundary.

## Failure handling

Do not skip, quarantine, weaken policy, or broaden the allowlist to obtain green tests. Classify the failure, update the finding/requirement, and invoke the named correction/recovery path when a foundational invariant fails.
