# SB12 Semantic Invariants

Status: Completed.

- Process runtime behavior is preserved by targeted dispatch-service integration tests.
- Process execution snapshots are owned by CanDoItAll.Processes.Contracts and stay neutral from AgentFramework, EF, and UI types.
- ProcessAutomationExecutionClient remains the adapter boundary for AgentFramework execution runtime details.
- No full Process Core, process driver-pack, EF entity move, Razor/UI move, or process tool rename was introduced.
- MAF/Core/Tooling product-module decoupling remains clean by source/project scan.
- No small, medium, mobile, tablet, Android, or iPhone viewport proof was created.
