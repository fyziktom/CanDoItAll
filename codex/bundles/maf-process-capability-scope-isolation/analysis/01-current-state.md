# Current State

## Summary

The user-reported leak is real. `WorkspaceRuntimePlugin` is a common workspace runtime plugin, but its image analysis prompt normalizers assume software-delivery screenshots and UI-state proof. The surrounding MAF/process architecture has a useful capability-policy foundation, but process scope is currently expressed through broad booleans and allowed operations rather than a first-class per-step capability contract.

## MAF Workspace Domain Leak

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`

- `AnalyzeImageFile` and `AnalyzeImageFiles` call `NormalizeImageAnalysisPrompt` and `NormalizeImageSetAnalysisPrompt`.
- Around line 566, the single-image default prompt says to describe visible UI state and defects.
- Around line 572, the wrapper prompt says the model receives one software-delivery screenshot image.
- Around line 581, the image-set prompt assumes ordered screenshots of the same software UI.
- Around line 596, the prompt tells the model to compare software UI labels, positions, grid rows, and UI state changes.

This behavior belongs in a software-delivery or UI-review capability, not in common workspace tooling. Common MAF can inspect/analyze images, but it must not decide that every image is a UI screenshot.

## Existing Capability Access Foundation

`repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs`

- `CapabilityExposureDescriptor` already models capability identity, runtime tool name, MCP server key, MCP tool name, tags, operation classifications, side effects, and availability.
- `CapabilityAccessRule` supports `Deny` and `Require` effects with selectors for kind, key, tag, operation classification, runtime tool, MCP server, MCP tool, and implementation key.

`repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs`

- Deny rules suppress candidates.
- Required capabilities are reported when missing or denied.
- Important nuance: `Allow` rules are not restrictive today. An allowlist UI or schema must not rely on `Allow` to remove unrelated capabilities unless the evaluator/compiler is deliberately extended.

## Current MAF Runtime Composition Path

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`

- The access planner builds descriptors for catalog capabilities and configured workspace tools.
- It calls `RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies`.
- It passes `RequiredCapabilities: []`, so required/forced process capabilities are not currently represented.
- Suppressed catalog skills become excluded context manifest entries under the skills source category.

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`

- Agent workspace-tool access settings compile to deny rules.
- `WorkspaceToolsEnabled=false` denies workspace/storage tools.
- `BrowserToolsAllowed=false` denies browser-access classifications.
- Governed process steps compile allowed operations into classification rules and deny configured workspace tools for steps without matching operations.

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs`

- Runtime tool-provider tools are converted to capability descriptors after each provider creates tools.
- They are filtered through the same evaluator.
- Provider key is not yet a first-class selector. Provider-level suppression needs provider-key tagging or implementation-key mapping before it is dependable.

## Current Process-To-MAF Handoff

`repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs`

- Process assignments serialize allowed operations, target scope, product mutation allowance, and browser proof allowance into execution metadata.

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

- `CreateRuntimeContextIntent` reconstructs `AgentRuntimeContextIntent` from execution metadata.
- The reconstructed intent contains source/run/step ids, allowed operations, browser allowance, scaffold-only flag, product mutation allowance, workspace profile, runtime tool-provider enabled flag, and workspace-tools enabled flag.

`repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`

- `ProcessRuntimeStepAssignment` persists the step prompt, allowed operations, operation target scope, and launch variables.
- It does not persist a scoped capability policy or typed instruction fragments.

`repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`

- Process steps expose `Notes`, contract summaries, `AllowedOperations`, and `OperationTargetScope`.
- There is no template field for suppressing or requiring skills, runtime tools, MCP servers, MCP tools, runtime providers, or scoped instruction fragments.

## Process Prompt Customization Today

`repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`

- This driver adds AgentFramework-specific process instructions to generic process step briefs.
- It is a valid module-side customization point, but it is prompt-only and cannot prevent unwanted skills/tools/MCPs from being attached.

## Architecture Conclusion

The fix is not to add more special prompts to `WorkspaceRuntimePlugin`. MAF needs a generic image analysis baseline and a stronger runtime capability scope mechanism. Processes then need typed step contracts that compile through the AgentFramework adapter into that mechanism.
