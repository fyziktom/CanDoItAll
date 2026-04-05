# Assumptions and Risks

## Assumptions

- The extracted code under review is the current post-phase-5 state.
- The upcoming plugin wave includes email, LinkedIn, and custom API style integrations with configuration, secrets, health checks, and policy exposure.
- The desired product direction is still mindmap-first and node-centric, not a separate task-only system.

## Critical Path Risks

- If the plugin wave starts before the remaining parallel truth is removed, new integrations will copy the wrong storage pattern.
- If node kind and assignment semantics are not centralized first, CRM/HR and connector work will spread more hidden rules across the codebase.
- If provider/resource extensibility stays enum-based, each new connector will increase switch-driven fragility.

## Validation Risks

- `dotnet` build/test/runtime validation could not be executed in this environment.
- Some behavior may be better or worse at runtime than static review alone can prove.
- The bundle is therefore a **prepared execution bundle**, not an implementation completion claim.

## Reopen Triggers

- Any future feature proposal that adds new foreign ids into Workbench metadata.
- Any proposal that introduces another system-managed canonical row type in Workbench tables.
- Any new connector implemented by expanding `ProviderKind`, `ResourceKind`, or editor switch maps.
- Any new node-scoped assignment role implemented without the central capability model.
