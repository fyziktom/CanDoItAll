# SB01 Semantic Invariants

## Invariant SB01-CATALOG-DI

- Invariant ID: `SB01-CATALOG-DI`
- Source raw note: RN01 and R1 require product save/import/publish/test paths to reject unknown, planned, unavailable, or schema-invalid executors before runtime dispatch.
- Expected behavior: Product DI registrations resolve `IWorkflowDefinitionValidator` with `IWorkflowExecutorCatalog`, and catalog services reject executor IDs that the catalog cannot resolve or cannot run.
- Disallowed shallow implementation: Keeping catalog-aware validation only in direct unit tests while `AddAgentFrameworkCore`, `AddAgentFrameworkModule`, or module template validation keep using the parameterless validator.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-hosting-validator-missing-catalog.txt`
- Passing test: `bundle://proof/SB01/transcripts/unit-hosting-validator-after-di-fix.txt`
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`; `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs`
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions-validator-di-registrations.txt`
- Red-team negative case: The failing-first test saves an active workflow containing `missing.executor` through the product catalog service and proves the old registration accepted it.
- Downstream dependency check: SB02 can start only after `bundle://proof/SB01/transcripts/unit-workflow-executor-validator-after-di-fix.txt` proves planned/unknown/schema validator coverage still passes with catalog-backed validation.

