# Receipt recovery composition repair

Status: source prepared in the managed runtime checkout on 2026-10-16. No build, test, database operation, static scan or commit performed here. The frozen main Stable run produced two matching stack captures and was stopped by the parent; its partial result is not a completed Integration pass.

## Confirmed cause

`ApiTestHost.ConfigureAgentRuntimeOverride` replaces `IAgentFrameworkWorkspaceService` with the direct `AgentFrameworkWorkspaceService`. The same shape exists in the execution-tracking and JSON-schema fixtures. Its constructor resolves `IEnumerable<IAgentToolReceiptReconciliationProvider>`. Composition's definition-provider alias resolved `ProjectDefinitionToolAdmission`, whose `ProjectStructureAgentAdmissionService` and `ProjectStructureAgentAuthorizationService` both require the workspace service. The factory alias hid the dependency cycle from normal constructor call-site validation. The repeated `AddCanDoItAllRuntimeModules` lambda `b__0_6` in both captured stacks corresponds to that definition-provider alias.

The production registration uses `CurrentProfileAgentFrameworkWorkspaceService`, whose constructor stores its workspace factory without constructing an aggregate. That constructor does not establish the same direct cycle. Removing the fixtures' direct workspace override alone would bypass their fake runtime: the production factory creates its MAF and scenario/process adapters directly. It would also bypass the execution-startup fixture's decorated store instrumentation. The repair therefore separates receipt recovery from mutation orchestration rather than changing those fixtures or removing providers.

## Source change and boundary

- `ProjectDefinitionToolReceiptRecovery` implements the existing reconciliation interface. It owns the moved exact receipt lookup, reservation lookup/validation, saved-session equality, reconciliation and result-envelope construction.
- `ProjectDefinitionToolAdmission` injects that reader and shares those exact methods. Capture, mutation authorization, reservation/grant, writer dispatch, active lease and result-disclosure checks remain with admission.
- Composition keeps admission registered for execution and registers recovery directly as the reconciliation implementation. The concrete recovery type is also registered for admission's dependency; it has no mutable per-operation state, cache or disposal ownership. These two scoped registrations may construct separate trivial reader instances with the same scoped dependencies; neither owns a second operation or workspace.
- The current-read dependencies are unchanged: `IAgentCatalogReadLeaseStore`, `ProjectProcessExecutionMutationService`, `ProjectWriteAdmissionService`, `ProjectDefinitionReceiptReader`. Catalog construction uses the bound profile/file catalog; Process execution reading resolves the sandbox execution store directly, and its target projection dependencies are database/catalog queries. The reviewed construction graph has no workspace-service edge on these paths.

Claim verification still occurs before and after lookup. Current Agent/Process read permission, exact reservation identity, target lifetime, caller/address, digest/result versions and historical receipt bytes are unchanged. No source effect lease is acquired merely to read a receipt. Effect kind `project-definition-operation`, envelope kind `project-definition-receipt`, version 1 and tool-name support remain unchanged. R1b's later bounded Process read work belongs in this recovery path, not the mutation admission constructor.

## Source-derived focused selection

| Existing/new source selection | Expected rows | Basis |
| --- | ---: | --- |
| Unit `ProjectDefinitionToolProposalCodecTests` | 12 | 9 Facts plus 3 inline rows; only reservation helper owner changed |
| Integration `AgentJsonSchemaOutputExecutionIntegrationTests` | 2 | 2 Facts, including one new composition regression |
| Integration methods in `ProjectStructureResultDisclosureIntegrationTests.DefinitionOperations.cs` | 6 | 3 tool rows, 2 source/lease rows, 1 actual MAF receipt recovery Fact |
| Integration `Registered_process_definition_tool_replays_original_receipt_after_dispatch_expiry_but_cannot_dispatch_a_fresh_effect` | 1 | Existing Fact |
| Focused total | 21 | 12 Unit and 9 Integration; no dynamic data providers in this selection |

The new Fact uses the actual `ApiTestHost.agentRuntimeOverride` path and all five real reconciliation providers, including the existing `HrSimpleChatRuntimeToolProvider` registered by `AddHrSimpleChatDefinitionTools`. A throwing mutation-admission registration makes an accidental recovery-to-mutation dependency fail immediately rather than hanging recursively. It verifies exact provider types and unique support for each definition tool, resolves the direct workspace aggregate and executes a deterministic fake response. The existing JSON-schema/API Fact additionally verifies the same real provider set. Existing owner recovery rows prove receipt behavior; the new smoke test does not claim to replace them.

Changed production files: Workbench `AgentTools/ProjectDefinitionToolAdmission.cs`, new `AgentTools/ProjectDefinitionToolReceiptRecovery.cs`, Composition `RuntimeHostServiceCollectionExtensions.cs`. Changed tests: Unit `ProjectDefinitionToolProposalCodecTests.cs`, Integration `AgentJsonSchemaOutputExecutionIntegrationTests.cs`. No test-host factory, runtime-port implementation, database schema, receipt protocol, source authority or current-read policy changed. Focused diff whitespace check passed; compilation and execution remain pending parent validation.
