# Source Artifacts

| Artifact | Purpose | Owning subbundles |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateCatalogInventory.cs` | Representative template inventory and family mapping. | SB02 |
| `repo://Templates/Processes/processes/software-delivery/definition.json` | Multi-team/software-delivery template definition. | SB02, SB04 |
| `repo://Templates/Processes/processes/business-plan-development/definition.json` | Business-analysis template definition. | SB05 |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` | Representative process automation E2E proof surface. | SB03, SB04 |
| `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs` | Business-plan PostgreSQL process proof surface. | SB05 |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | Outbox persistence, draining, and automation dispatch handoff. | SB03, SB04, SB05 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch` | Automation dispatch, finalizer, runtime-host, and read-only verification implementation. | SB03-SB07 |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | Code-first ratio and boundary guard proof surface. | SB01, SB08 |
