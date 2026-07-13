# Breaking-Change Risk Map: MAF 1.8 to 1.13

This map translates known MAF 1.9-1.13 release-note changes into CanDoItAll source risks.

## Highest-risk areas

| CanDoItAll area | Why it is high risk |
| --- | --- |
| `Runtime/Capabilities/RuntimeCapabilityComposer.cs` | Attaches skills, context providers, A2A, compaction, workspace tools, registered runtime tool providers, and provider-native tools. Recent MAF releases changed skills approval/caching/disposal and FileAccess/FileMemory APIs. |
| `Runtime/MafRuntimeAgentFactory.cs` | Builds `ChatClientAgentOptions`, uses `AIAgent.AsBuilder()`, middleware, `ApprovalRequiredAIFunction`, logging, OpenTelemetry, finalizer capture, and tool policy enforcement. |
| `Runtime/MafRuntimeSessionBuilder.cs` | Uses `AgentSession`, `DeserializeSessionAsync`, `ChatClientAgentRunOptions`, `ResponseContinuationToken`, `ChatOptions.ResponseFormat`, and service/framework-managed history behavior. |
| `Runtime/Providers/MafProviderStreamingRunner.cs` | Uses overloads of `AIAgent.RunStreamingAsync`, `AgentResponseUpdate`, `ToolApprovalRequestContent`, `ToolCallContent`, `FunctionCallContent`, `McpServerToolCallContent`, and `AdditionalPropertiesDictionary`. |
| `Runtime/MafAgentRuntime.cs` | Coordinates streaming, approvals, finalizer repair, provider failure handling, usage observations, and serialized session state. |
| `Workflows.MafAdapter` project | Directly references `Microsoft.Agents.AI.Workflows`. Workflow APIs may change around checkpointing/resume/declarative options. |

## Release-risk translation

| MAF release area | Risk to CanDoItAll | Phase-1 handling |
| --- | --- | --- |
| Microsoft.Extensions.AI dependency floor moved to `10.6.0` | Direct `10.5.1` reference in `Workflows.MafAdapter` can produce downgrade warnings or compile mismatches. | Bump `Microsoft.Extensions.AI.Abstractions` to `10.6.0`; add explicit `Microsoft.Extensions.AI` only if restore/build requires it. |
| Dependency floor for `Microsoft.Extensions.DependencyInjection.Abstractions` moved to `10.0.9` | Direct `10.0.7` reference can produce downgrade warnings. | Bump to `10.0.9`. |
| `AgentSkillsProvider` tools require approval by default | CanDoItAll already has policy gates and approval suppression paths. Behavior could become more restrictive or duplicate approval wrapping. | Preserve current approval policy. Add/fix adapter code only where compile or focused tests show drift. Validate skill tools and process-scoped runs. |
| Skill-source caching extracted and skill sources disposable | `RuntimeCapabilityComposer.AttachSkillsAsync` and async disposable collection may need API updates. | Fix compile errors in skill attachment. Preserve `RuntimeCapabilityState.AsyncDisposables` cleanup. Do not redesign skills. |
| New `AgentSkillsSourceContext` parameter | Skill-source implementations/adapters may require new method signature. | Add context parameter handling with minimal defaults. Do not change catalog semantics. |
| FileAccess/FileMemory API alignment and file editing tools | Workspace/file tools or memory-backed tools may compile-break or change approval requirements. | Fix adapters. Keep existing workspace scope, approval, and audit policy. Do not expose new file editing tools unless existing capabilities already request them. |
| OpenAI Hosting `OptionsMapping` refactor | Likely irrelevant if repo does not use `Microsoft.Agents.AI.Hosting` or Foundry hosting packages. | Do not adopt hosting. If compile errors appear through OpenAI adapter only, apply minimal option-mapping fix. |
| A2A default session store changed to noop | A2A remote tool behavior may lose persisted session unless explicitly configured. | Keep current A2A surface. Add explicit session store only if tests/source prove it is required for current behavior. |
| Checkpoint resume fixes after package upgrades | Good for future workflow/process reliability, but no feature adoption in phase 1. | Only validate existing workflow adapter tests. |
| Structured-output behavior using last message only | Can affect finalizer/structured output if current code relies on earlier messages. | Keep required finalizer as primary governed output path. Add regression tests for finalizer being last significant tool invocation. |
| OpenTelemetry `execute_tool` spans and function-invoking client placement | May alter trace naming or order. | Preserve current telemetry assertions only if tests require updates. Do not weaken audit tags. |

## Compile-break triage order

1. Package restore/downgrade warnings.
2. Missing/renamed types in `Microsoft.Agents.AI` or `Microsoft.Extensions.AI`.
3. Skill-source and approval-related signature changes.
4. FileAccess/FileMemory signature changes.
5. `AIAgent.RunStreamingAsync` overload changes.
6. Session serialization/deserialization changes.
7. Workflow adapter API changes.
8. Test expectation updates.

## Behavioral invariants for every fix

For each compile fix, Codex must explicitly check:

- Does this change preserve current approval behavior?
- Does this change preserve finalizer capture and required finalizer repair behavior?
- Does this change preserve process-scoped context filtering?
- Does this change preserve provider lane gates and timeouts?
- Does this change preserve serialized session state compatibility or fail clearly?
- Does this change preserve runtime tool ownership tracing?
- Does this change preserve no direct process runtime tool provider?

If any answer is not clearly yes, stop and isolate the change behind the smallest existing abstraction.
