# Capability Access Policy Architecture

## Problem Statement

Processes, workflows, agents, and UI setup must be able to limit or forbid selected skills, tools, MCP servers, and MCP tools. The current codebase has partial governance in separate places:

- Required capability checks are handled by `AgentCapabilityRequirementEvaluator`.
- Process tool permissions are handled by `ProcessToolOperationAuthorizer` and string operation names.
- MAF runtime composition has hardcoded attach-time filters, including process-step skill exclusion and workspace-tool gating.
- Agent and template settings expose coarse flags such as tool usage or workspace tool profiles.

Those mechanisms are useful signals, but they are not one reusable access model. The migration must add a typed capability access layer before MAF reconnection so every capability kind is filtered by the same contracts and diagnostics.

## Microsoft Learn Grounding

The design should borrow the useful parts of ASP.NET Core policy-based authorization without coupling the capability core to ASP.NET:

| Guidance | How this bundle applies it | Source |
| --- | --- | --- |
| Policy authorization uses requirements and handlers to create reusable, testable authorization logic. | Use typed capability access rules, selectors, and evaluator handlers registered through DI instead of hardcoded `if` chains in MAF. | `https://learn.microsoft.com/aspnet/core/security/authorization/policies?view=aspnetcore-10.0` |
| Resource-based authorization accepts a resource and requirements. | Evaluate access against a capability exposure descriptor as the resource, plus process/workflow/agent policy requirements. | `https://learn.microsoft.com/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0` |
| Options validation supports startup validation and typed configuration binding. | Template/UI policy DTOs must validate at load/save time; invalid access policy must block materialization instead of falling back. | `https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0` |
| `System.Text.Json` supports enum string converters and custom converter factories. | Template text is converted to typed enums and value objects at the boundary; runtime logic must not compare raw strings. | `https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings` and `https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/converters-how-to#custom-converter-patterns` |

## Core Model

### Candidate Set

The access policy starts from capabilities already assigned to the agent and enabled by the runtime context. A policy can restrict or require those candidates. It must not grant capabilities the agent did not already have unless a future, separately audited grant mechanism is explicitly designed.

This prevents a process or workflow template from escalating an agent just because it contains an `allow` rule.

### Capability Exposure Descriptor

Every skill, tool, MCP server, and MCP tool must expose a common descriptor:

- `CapabilityIdentity` with typed `CapabilityKind` and `CapabilityKey`.
- `ImplementationKey`, `RuntimeToolName`, `McpServerKey`, `McpToolName`, or skill source identity where applicable.
- `Tags` normalized through typed tag values.
- `OperationClassifications`, such as read, write, mutation, validation, script execution, browser access, project structure, document processing, or provider-native.
- `SideEffectProfile`, approval defaults, and setup-test status where applicable.
- Source metadata for diagnostics, such as template path, source kind, or implementation binding.

New capability implementations automatically participate in restrictions when they provide this descriptor. The evaluator must not require code changes for each new concrete tool, skill, or MCP.

### Policy DTOs And Domain Types

Template and UI contracts should use DTOs that are friendly to JSON and editors, then compile into domain types:

- `CapabilityAccessPolicyTemplateDto`
- `CapabilityAccessRuleTemplateDto`
- `CapabilitySelectorTemplateDto`
- `CapabilityAccessPolicy`
- `CapabilityAccessRule`
- `CapabilitySelector`
- `CapabilityAccessEvaluationContext`
- `CapabilityAccessEvaluationResult`
- `EffectiveCapabilitySet`
- `SuppressedCapabilityDiagnostic`

Runtime code uses strongly typed wrappers:

- `CapabilityKind`
- `CapabilityKey`
- `CapabilityTag`
- `RuntimeToolName`
- `McpServerKey`
- `McpToolName`
- `ImplementationKey`
- `ProcessOperationKey`
- `CapabilityAccessEffect`
- `CapabilitySelectorKind`
- `CapabilityAccessScope`

Use parser/formatter helpers or converters for all text boundaries. Invalid template or UI text must return structured validation errors with path/key/field and repair guidance.

### Supported Selectors

Keep selectors expressive enough for product needs but bounded enough to test:

| Selector | Purpose | Notes |
| --- | --- | --- |
| `All` | Disable all capabilities in a scope. | Used sparingly for locked process steps. |
| `Kind` | Disable all skills, all tools, or all MCPs. | Strong enum, not raw text. |
| `CapabilityKey` | Target a stable catalog entry. | Primary precise selector. |
| `Tag` | Target grouped capabilities, for example `mutation` or `external`. | Tags come from descriptors and templates. |
| `OperationClassification` | Match behavior categories such as validation, write, script, browser, document, or project structure. | Replaces process string operation coupling. |
| `RuntimeToolName` | Compatibility selector for provider/tool names exposed to models. | Parsed to a value object. |
| `McpServerKey` | Target a server. | Applies to server and optionally all child tools. |
| `McpToolName` | Target a discovered MCP tool. | Requires server context to avoid ambiguous names. |
| `ImplementationKey` | Target a concrete internal implementation when catalog key is not enough. | Useful for admin diagnostics and migration. |

Do not support regex, embedded scripts, arbitrary C# expressions, or unbounded policy languages in templates.

### Effects And Precedence

Use deterministic precedence:

1. Retired, unavailable, or failed setup capabilities are excluded before policy evaluation.
2. System-deny rules win over every other rule.
3. Explicit deny wins over explicit allow in all lower scopes.
4. Required capabilities must exist in the candidate set and must not be denied.
5. Allow keeps an existing candidate; it does not create a new assignment.
6. `Inherit` is allowed only in template/UI DTOs. The compiled domain policy should treat it as no rule or an explicit default inherited from the parent scope.

Recommended scope order:

1. System policy.
2. Agent/team default policy.
3. Workflow definition policy.
4. Workflow node policy.
5. Process definition policy.
6. Process step policy.
7. Runtime/UI override policy.

The evaluator should produce both an allowed set and suppressed diagnostics so MAF manifests and UI can explain why a capability was not attached.

## Runtime Integration

Add `ICapabilityAccessPolicyEvaluator` to the capability abstraction layer. The evaluator accepts:

- Candidate descriptors from the typed registry.
- Effective policy rules from agent, workflow, process, and runtime context.
- Existing required-capability requests.
- Setup availability and retired capability state.

It returns:

- `EffectiveCapabilitySet` containing allowed skill/tool/MCP descriptors.
- `SuppressedCapabilityDiagnostic` entries with rule scope, selector, effect, capability identity, and repair hint.
- Required-capability diagnostics for missing or denied capabilities.

`AgentRuntimeContextIntent` should evolve from coarse booleans and `IReadOnlyList<string> AllowedOperations` toward a typed policy snapshot or `EffectiveCapabilityAccessContext`. Current fields can remain as compatibility inputs, but they should be parsed once into typed `ProcessOperationKey` or operation classifications.

MAF attachment must consume `EffectiveCapabilitySet` rather than reapplying private filters. Existing sources such as `ShouldExcludeSkillsForProcessStep`, `ResolveProcessScopedWorkspaceToolAccess`, and `ProcessToolOperationAuthorizer` should be folded into compatibility adapters or evaluator rules.

## Template And UI Shape

Templates should support optional policy blocks on:

- Agent/team member templates.
- Process definitions.
- Process steps.
- Workflow definitions.
- Workflow nodes.
- Capability templates for default tags and operation classifications.

Example shape:

```json
{
  "capabilityAccessPolicy": {
    "defaultEffect": "inherit",
    "rules": [
      {
        "effect": "deny",
        "scope": "processStep",
        "selector": {
          "kind": "operationClassification",
          "value": "mutation"
        },
        "reason": "Validation step cannot mutate product files."
      },
      {
        "effect": "deny",
        "scope": "processStep",
        "selector": {
          "kind": "capabilityKey",
          "value": "workspace-write-file"
        },
        "reason": "Read-only analysis step."
      }
    ]
  }
}
```

The UI should edit the typed DTO through catalog-backed choices, not raw strings:

- Segmented control for capability kind.
- Searchable selector for capability key.
- Tag chips from the catalog.
- Operation classification checkboxes.
- MCP server/tool picker after setup list-tools succeeds.
- Read-only diagnostic preview showing which capabilities will be suppressed.

The advanced raw JSON editor can exist only as a secondary path with the same validation and diagnostics.

## Error And Diagnostics Requirements

Every policy failure must be repairable:

- Invalid selector text: include template path, JSON field path, bad value, allowed examples, and repair hint.
- Unknown capability key: include nearest known keys if cheap and bounded.
- Ambiguous MCP tool name: include server context requirement.
- Denied required capability: include required source, deny rule scope, selector, reason, and suggested template/UI location to edit.
- Suppressed runtime attachment: include capability identity, rule scope, effect, and reason in `AgentRuntimeContextManifestSource.Excluded`.

Do not collapse these into messages such as `Capability denied` or `MCP unavailable`.

## Test Strategy

### Unit Tests

- Parse/format every enum and value object; invalid values fail with structured diagnostics.
- Rule precedence: system deny, deny-over-allow, required-plus-denied, inherited defaults.
- Selector matching for key, kind, tag, operation classification, runtime tool name, MCP server, and MCP tool.
- A fake newly registered capability descriptor is suppressed by tag/kind without evaluator code changes.
- Allow does not grant an unassigned capability.
- Error messages mask sensitive values and include repair hints.

### Integration Tests

- Process step policy suppresses a tool, a skill, an MCP server, and an MCP tool through the same evaluator.
- Existing process `AllowedOperations` parse into typed classifications and preserve current behavior.
- MAF runtime composition attaches only the effective set and records excluded sources.
- Required-capability evaluator produces denied-required diagnostics when a required capability is suppressed.
- Template materialization rejects invalid access policies before seeding.

### Component And API Tests

- UI policy editor round-trips typed DTOs.
- API validation rejects invalid selectors and unknown keys.
- Capability preview shows allowed and suppressed counts with actionable details.
- Existing agent capability panel still works when no policy is configured.

### E2E Tests

- A read-only process step forbids mutation tools and still allows validation tools.
- A workflow node denies external MCPs but keeps internal hosted MCPs.
- A denied required capability blocks the run with a repairable diagnostic.
- Setup UI can test MCP list-tools, select an allowed MCP tool, then deny it in a process step and show the expected suppression.

## Performance And Maintainability Guardrails

- Compile templates and UI DTOs into immutable domain policies once per materialization/runtime context.
- Do not parse JSON or text selectors per tool call.
- Cache normalized descriptors and selectors with bounded invalidation.
- Keep evaluator files split by selector matching, precedence resolution, diagnostics, and template conversion if they grow.
- Do not use reflection over concrete tool/skill/MCP types for policy matching.
- Keep logs structured and mask any selector value that could contain user-provided secrets, URLs with credentials, or raw command arguments.
