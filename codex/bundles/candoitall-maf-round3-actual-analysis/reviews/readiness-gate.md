# Readiness Gate

Round 3 is ready only when every item below is true.

## Security

```bash
rg -n --hidden --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/.git/**' 'sk-[A-Za-z0-9_-]{20,}' .
```

This must return no real secrets. Placeholders are allowed only if they do not match a realistic key.

## Build and tests

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

Focused filters should include:

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "FullyQualifiedName~AgentToolInvocationPolicy|FullyQualifiedName~AgentFinalizer|FullyQualifiedName~ProviderFeatureMatrix|FullyQualifiedName~AgentOutputContract|FullyQualifiedName~ProcessRunAutomation|FullyQualifiedName~Recovery|FullyQualifiedName~Rework|FullyQualifiedName~Secret"
```

## Required evidence

- No plaintext secret remains.
- Process mutation tools classify as mutation.
- Process mutation after finalizer fails sequence validation.
- Required finalizer missing/invalid cannot complete governed step.
- QA rejection creates typed rework packet.
- Manual rerun attaches typed packet or explicitly creates human-directed packet.
- Format repair does not create new agent run.
- Provider failure uses fresh session/fallback rules.
- Approval continuation uses same compatible session.
- Proof reuse is fingerprint-based.
- Verification docs list only commands/tests that actually ran.

## Remaining-risk format

If any gate cannot be satisfied, document:

- exact missing gate;
- why it is missing;
- expected fix;
- risk level;
- whether it blocks production.

## Execution results

Captured: 2026-04-27.

- `dotnet --info`: passed. SDK 10.0.203, host 10.0.7, MSBuild 18.3.3, Windows 10.0.26200 win-x64.
- `dotnet restore CanDoItAll.slnx`: passed with existing NU1510, NU1902, and NU1904 warnings.
- `dotnet build CanDoItAll.slnx --configuration Release --no-restore`: passed with 0 errors and 56 warnings.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests|FullyQualifiedName~AgentRuntimeHardeningStaticRegressionTests|FullyQualifiedName~SecretScanningTests"`: passed, 68/68.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AgentRecoveryModelsTests|FullyQualifiedName~MafAgentRuntimeTests"`: passed, 37/37.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"`: passed, 132/132.
- `git grep -l "sk-[A-Za-z0-9_-]\{20,\}" -- . ":!**/bin/**" ":!**/obj/**" ":!**/.git/**"`: no tracked-file matches.
- PowerShell all-worktree scan excluding `bin`, `obj`, `.git`, and `node_modules`: no matches. A broader dependency-inclusive scan reported generated Tailwind `node_modules` package false positives; those files are not tracked source.
- `dotnet test CanDoItAll.slnx --configuration Release --no-build`: failed on existing broad-suite failures outside this bundle scope. See `reviews/execution-report.md`.

Security note: the exposed provider key must still be rotated or revoked outside the repository; source removal cannot invalidate an already exposed credential.
