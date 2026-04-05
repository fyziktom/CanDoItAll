# Rollout And Rollback Notes

## Final QA Gate

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -nologo -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrNavigationTests|FullyQualifiedName~CrmHrDirectoryPageTests|FullyQualifiedName~CrmPageTests|FullyQualifiedName~CrmHrWorkforcePageTests|FullyQualifiedName~RecruitingPageTests|FullyQualifiedName~AiAgentsPageTests|FullyQualifiedName~AssignmentsPageTests|FullyQualifiedName~ProjectsCrmHrIntegrationTests|FullyQualifiedName~CrossModuleResponsiblePartyPageTests|FullyQualifiedName~CrmHrPrivacyBoundaryTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrSchemaIntegrationTests|FullyQualifiedName~DatabaseMigrationIntegrationTests|FullyQualifiedName~CrmInteractionIntegrationTests|FullyQualifiedName~OpportunityConversionIntegrationTests|FullyQualifiedName~WorkforceProfileIntegrationTests|FullyQualifiedName~StaffingAllocationIntegrationTests|FullyQualifiedName~RecruitmentLifecycleIntegrationTests|FullyQualifiedName~AiAgentProfileIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests|FullyQualifiedName~CrmHrCrossModuleIntegrationTests|FullyQualifiedName~CrmHrAuditTrailIntegrationTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -nologo -v minimal --filter "FullyQualifiedName~CrmHrShellSmokeTests|FullyQualifiedName~CrmHrRegressionTests"`
- `python C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\scripts\validate_bundle.py C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final --profile initiative --stage prepared`

## Migration Rehearsal

- The final integration gate includes `CrmHrSchemaIntegrationTests` and `DatabaseMigrationIntegrationTests`, which prove fresh database bootstrap and migration application through the current test host.
- The final solution build compiles both `CanDoItAll.Migrations.Sqlite` and `CanDoItAll.Migrations.PostgreSql`, so both migration assemblies participate in the closure gate.

## Rollout Notes

- Deploy the app using the normal runtime bootstrap path so the existing database-migration startup flow applies the CRM-HR schema changes.
- After deployment, smoke the final route set: `/crm-hr`, `/projects`, `/activity`, `/resources`, `/validation`, and `/test-lab`.
- Use the B13 browser evidence and screenshot review as the expected visual baseline for the final shell and cross-module workspaces.

## Rollback Notes

- Take a database backup or snapshot before applying the deployment that carries the CRM-HR schema changes.
- If rollback is required, restore the previous application package together with the pre-deployment database backup; do not leave a newer app schema paired with an older app binary.
- Re-run the standard route smoke after rollback to confirm `/crm-hr`, `/projects`, `/activity`, `/resources`, `/validation`, and `/test-lab` return to their prior stable state.
