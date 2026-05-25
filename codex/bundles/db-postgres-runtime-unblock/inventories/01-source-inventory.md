# Source inventory

## Primary files to inspect/change

- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileStartupConnectionResolver.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs
- repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs
- repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs
- repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferService.cs
- repo://src/CanDoItAll.Modules.Automation/**
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/**
- repo://src/CanDoItAll.Modules.SchedulerPlanner/**
- repo://tests/CanDoItAll.Tests.Unit/**
- repo://tests/CanDoItAll.Tests.Components/**
- repo://tests/CanDoItAll.Tests.Integration/**
- repo://tests/CanDoItAll.Tests.Playwright/**

## Existing proof reports to review

- repo://codex/bundles/db-remove-sqlite-followup-bundle-v1/reviews/01-execution-report.md
- repo://codex/bundles/postgresql-only-main-runtime-bundle-v1/reviews/01-execution-report.md

## Diff artifacts that need policy decision

- repo://.codex/bundles/project-structure-workflow-runs/proof/**
- repo://codex/bundles/postgresql-only-main-runtime-bundle-v1/**
- repo://codex/bundles/db-remove-sqlite-followup-bundle-v1/**
