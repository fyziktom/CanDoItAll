# SB04 proof manifest

## Status

Completed.

## Owned requirements

Separate canonical runtime context creation from explicit profile-specific maintenance context creation.

## Changed files

- `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferService.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`
- `repo://src/CanDoItAll.Modules.Workbench/DatabaseTransfer/ProjectPackageService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB04/transcripts/profile-context-source-audit.txt`
- `bundle://proof/SB08/transcripts/focused-integration-tests.txt`
- `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`

## Source assertions

- `ISwitchableAppDbContextFactory` was renamed to `IProfileAppDbContextFactory`.
- Runtime modules use `IDbContextFactory<AppDbContext>` for canonical runtime contexts.
- Profile-specific contexts remain in schema health/bootstrap, transfer, and package import/export paths.

## Semantic positive proof

The focused integration suite validates the profile-specific maintenance paths touched by this work, and the source audit lists every profile-specific factory use site.

## Adversarial negative proof

The source audit shows no process/automation runtime loop has been converted to profile-specific context creation.

## Residual risks

The audit is source-based, not a Roslyn analyzer committed to the repo. It is sufficient closure proof for this bundle but should be converted into a guard test if this area changes frequently.
