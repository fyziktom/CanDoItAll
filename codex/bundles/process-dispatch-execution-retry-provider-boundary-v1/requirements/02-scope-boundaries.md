# Scope Boundaries

## In Scope

- Module-local refactoring under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- Focused helper/coordinator extraction from `Execution.cs` and `Concurrency.cs`.
- Tests and architecture guardrails proving parity.
- Documentation-only driver-readiness map for execution/retry/provider recovery evidence families.

## Out of Scope

- Creating `CanDoItAll.Processes.Core`.
- Moving EF entities or persistence configuration.
- Creating production process driver APIs such as `IProcessDriverPack`, `IProcessDriverRegistry`, `IProcessHelperDriver`, or driver packages.
- Replacing AgentFramework/MAF execution integration.
- UI refactoring or browser/mobile proof.
- Behavioral changes to retry counts, provider fallback policy, recovery journals, no-progress compression, or completion decisions.
