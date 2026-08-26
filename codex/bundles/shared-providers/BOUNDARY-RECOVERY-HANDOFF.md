# Provider boundary recovery handoff

The completed boundary implementation spans `c0a26a6e264e5e56576372630e44ff0576d4692a^..9573b401d72c028204ba5db1128b671455e3891b`; this note is finalized by the `BR08: hand off corrected boundary to shared provider SB07` checkpoint.

`CanDoItAll.Modules.ProviderManagement` is the canonical owner of personal-provider and shared-provider control-plane behavior. The Workspace ownership statements in the original SB00 and SB02 architecture documents are historical evidence and are superseded by this recovery bundle; those documents are intentionally not rewritten.

Original SB07 must continue from the corrected branch HEAD and read this note before its own README. Its setup and orchestration must use ProviderManagement services through the Web endpoint mapping. It must not recreate Workspace provider services or DI registrations, and execution must use the unified MAF-backed provider execution port rather than a direct-inference bypass.

The physical table names `WorkspaceProviderSecrets`, `WorkspaceProviderProfiles`, `WorkspaceSharedProviderPublications`, `WorkspaceSharedProviderSources`, `WorkspaceSharedProviderImports`, and `WorkspaceSharedProviderInvocationRecords` intentionally remain historical for database compatibility. They do not indicate current logical ownership.

## Deferred Docker-dependent validation

This recovery bundle did not execute either command below because its Docker/Podman authorization is `DENIED_FOR_THIS_BUNDLE`:

```powershell
dotnet test tests/Solutions/CanDoItAll.Tests.Integration.slnx -c Release --no-build --no-restore --filter 'FullyQualifiedName~SharedProviderPersistenceIntegrationTests' --nologo --verbosity minimal /m:1
pwsh -NoProfile -File tools/SharedProviders/Run-SharedProviderE2E.ps1 -Reset
```

The first command provisions real PostgreSQL through Docker Compose. The second owns the original SB07 application-image build and three-instance lifecycle. Neither command is authorized by this handoff: the original SB07 Docker retry budget, replacement-run authority, and durable budget amendment remain separate prerequisites exactly as recorded in its status and `proof/test-budget-exception.md`.
