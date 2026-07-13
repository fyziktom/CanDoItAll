# C# Pattern Selection Records

## PSR-001 Scoped Policy Compiler

- Pattern: compiler/translator.
- Use for: mapping process-neutral directives to MAF capability access policies.
- Why: process templates should not know MAF selector classes, but MAF should receive strongly typed rules.
- Avoid: ad hoc JSON parsing in the runtime composer.
- Target owner: `CanDoItAll.Modules.Processes` for process-to-MAF translation; MAF may also have an internal compiler for runtime override DTOs.

## PSR-002 Descriptor Catalog Extension

- Pattern: descriptor catalog.
- Use for: adding provider-key/implementation-key/tag metadata to runtime tool-provider descriptors.
- Why: provider-level suppression requires stable selectable identity.
- Avoid: matching provider display names or concrete class names.
- Target owner: `RuntimeToolProviderComposer` and AgentFramework tooling descriptors.

## PSR-003 Policy Evaluator Reuse

- Pattern: policy evaluator.
- Use for: final suppression and required-capability decisions.
- Why: MAF already has `CapabilityAccessPolicyEvaluator` and diagnostics.
- Avoid: building a separate process-only suppression engine.
- Target owner: `CanDoItAll.AgentFramework.Capabilities.Access`.

## PSR-004 Scoped Instruction Contributor

- Pattern: contributor/composer.
- Use for: adding process-step instruction fragments only after scope validation.
- Why: instruction text and capability policy must share one contract.
- Avoid: appending raw process notes that conflict with suppressed capabilities.
- Target owner: process application/module integration.

## PSR-005 Dedicated Domain Tool Package

- Pattern: plugin/provider isolation.
- Use for: development-specific UI screenshot analysis behavior.
- Why: common workspace image tools must remain reusable across domains.
- Avoid: flags inside `WorkspaceRuntimePlugin` like `UseDevelopmentPrompt`.
- Target owner: a dedicated development tools project or module-owned runtime tool provider.
