# Current State

## Run Diagnosis

Run `6f0d229f-7c7e-4322-8b73-614ba5910cc4` is blocked at `qa-recheck`. The blocker is not a simple missing Playwright installation. The final QA recheck attempt attached `Playwright Local MCP` and workspace runtime tool providers, but the attempt did not call the proof tools that the step outcome itself listed as missing. A prior QA validation attempt in the same process did call `workspace_dotnet_run`, browser navigation, screenshot, console, image analysis, and cleanup tools successfully.

The immediate failure pattern is therefore:

- Step prose requested current runtime/browser/image proof.
- Allowed operations were broad enough to make browser/runtime proof possible.
- `CapabilityScopeJson` was `{}` for all relevant assignments.
- The agent returned or recovered an artifact-only outcome.
- Finalization did not reject that outcome based on missing required current-run receipts.
- Retries repeated the same artifact-reading behavior instead of forcing proof capture, reassignment, or manager fallback.

## Current Capability Model

`ProcessCapabilityScope` already supports directives and instruction fragments. It can express `Allow`, `AllowOnly`, `Deny`, and `Require` over targets such as capability kind, key, identity, tag, runtime tool name, runtime tool provider key, MCP server key, MCP tool name, implementation key, and operation classification.

The current translator maps this into `AgentRuntimeCapabilityScopeOverride`, but `Require` is only represented as `RequiredCapabilities` when the target kind is `CapabilityIdentity`. Required runtime tool and MCP tool receipt expectations are not modeled as first-class step proof requirements.

## Current Instruction Channel

Process-scoped instruction fragments are appended into the step brief. This is the right ownership direction because process templates own domain-specific guidance. The problem is that proof requirements remain prose-only. Prose can guide the agent, but it cannot safely drive HR readiness, tool suppression, runtime gating, or manager fallback.

## Current HR Matching Gap

The project-structure process assignment flow evaluates role fit and allowed operations. It does not appear to evaluate a typed step proof contract because the run assignments had empty capability scopes and no separate required-receipt model. In this case the selected `Delivery QA Observer` was broadly plausible and had browser/image capabilities, but the dialog could not prove that every required step receipt was available and enforced.

## Current Fallback Gap

The recovery/finalizer path can synthesize or accept structured outcomes from artifacts after provider timeout or failed attempts. That is useful for missing artifacts, but it is unsafe for required proof receipts. A process manager fallback must know whether the missing item is a recoverable artifact, missing tool access, missing capability assignment, missing current-run receipt, or unavailable external environment. Those are different decisions.

## Current Architecture Pressure

Several process and MAF classes remain large and responsibility-heavy. The work should not add more conditional logic into `AgentToolInvocationPolicy`, `AgentFrameworkWorkspaceExecutionService`, or process UI code. The implementation should extract focused services around contract compilation, readiness evaluation, receipt gating, and fallback planning while keeping MAF process-agnostic.
