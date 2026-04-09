# PRM-F13 — Future AgentFramework adapter and AI executor seam

## Objective

Prepare the process module for later Microsoft Agent Framework integration without forcing that runtime into the first process-management implementation.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F03, PRM-F05, PRM-F06, PRM-F07**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- The process runtime can distinguish manual, AI, and hybrid executor modes.
- The process module compiles and works without referencing AgentFramework projects.
- A bridge contract exists for future AI execution and handoff orchestration adapters.
- CRM-HR remains the durable owner of AI agent identity and staffing.
- AI-oriented role templates can later carry optional bridge hints without becoming a compile-time dependency now.

## Non-goals

- Do not reference AgentFramework projects from the first core process module build.
- Do not let runtime-specific details leak into the canonical process definition model.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/IProcessActorExecutionBridge.cs (new)`
- `src/CanDoItAll.Modules.Processes/NullProcessActorExecutionBridge.cs (new)`
- `src/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/AgentModels.cs (reference seam)`
- `src/CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Core/Contracts.cs (reference seam)`
- `src/CanDoItAll.AgentFramework-main/integration-map/*.md (reference seam)`
- `tests/CanDoItAll.Tests.Unit/ProcessActorExecutionBridgeTests.cs (new)`
