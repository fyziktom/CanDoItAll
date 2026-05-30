# SB02 Semantic Invariants

- Invariant ID: SB02-COST-PROPAGATION
- Source raw note: Provider prices are needed to calculate process run and workflow run cost correctly in analytics.
- Expected behavior: Agent run usage calculates cost from separate input, cached-input, and output token prices, and process/workflow analytics receive that cost where usage metrics exist.
- Disallowed shallow implementation: Updating only a displayed estimate or using a single blended token price is not acceptable.
- Failing-first test: N/A process exemption; no pre-existing process/workflow cost propagation test was available in the prepared bundle, so focused pricing math coverage and source proof were added.
- Passing test: ProviderPricingTests verifies separate cached token math and pricing metadata availability used by runtime cost paths.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`, and `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`.
- Production assertions: Finalized process steps synchronize actual cost from execution metrics, live process observation reads usage cost, and workflow event payloads carry usage cost.
- Red-team negative case: A missing model price cannot be hidden by a silent zero-cost validation path for configured manual model overrides.
- Downstream dependency check: Process history, live process pages, and workflow run event consumers receive calculated usage cost without changing estimated cost semantics.
