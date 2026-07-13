# Assumptions And Risks

## Assumptions

- Common MAF must remain reusable outside software-delivery processes.
- Processes are intended to support multiple domains, not just development workflows.
- Process core/template/runtime projects should remain independent of MAF implementation projects.
- It is acceptable for the AgentFramework process adapter module to translate process-neutral scope declarations into MAF capability access rules.
- Existing capability access descriptors and evaluator should be reused where possible.
- Existing process operation contracts remain valid, but they are not enough for named skill/MCP suppression.

## Critical Path Risks

- Validator-facing summary: the critical path risk is fail-open policy behavior or misplaced ownership that makes later process suppression unreliable.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Treating `Allow` as restrictive | Capabilities remain attached even though a process author expected an allowlist | Implement restrictive semantics explicitly or compile process allow-only directives into deny rules for non-matching candidates. |
| Process contracts reference MAF types directly | Process runtime becomes coupled to one agent runtime | Define process-neutral contracts and map them in `CanDoItAll.Modules.Processes` AgentFramework integration. |
| Provider-level suppression has no stable selector | Runtime tool providers can still create unwanted tools | Add provider-key or implementation-key tagging to provider-generated descriptors before provider-level policies are exposed. |
| Domain prompt text is moved but still globally seeded | The leak persists through template or agent defaults | Inventory templates, seed capabilities, and agent instructions for development-only image guidance before closure. |
| Metadata parsing fails open | Invalid process scope silently attaches default capabilities | Scope metadata parse/validation must throw or block governed execution with actionable diagnostics. |
| Process prompts and capability policy diverge | Agent receives instructions for a tool that was suppressed or lacks required instructions | Build scoped prompt fragments from the same validated process-step scope contract used for MAF policies. |

## Validation Risks

- Validator-facing summary: validation must prove capabilities are actually absent from context, not merely discouraged by prompt text.

| Risk | Required Proof |
| --- | --- |
| Suppressed skill still appears in assembled context | Context manifest and effective capability descriptor assertions for a management-only step. |
| Suppressed MCP server still lists tools | MCP attachment tests proving server/tool descriptors are denied before use. |
| Runtime provider suppression only hides provider after tool creation | Provider descriptor/candidate diagnostics proving denied tools are not attached; provider creation side effects must be audited. |
| Generic image analysis loses useful evidence | Unit tests proving prompt is domain-neutral and still includes deterministic image evidence when available. |
| Process step scope is not persisted | Assignment store tests and persistence mapping tests for new scope fields. |

## Reopen Triggers

- Any common MAF project still contains software-delivery, UI-review, Blazor, browser-proof, or screenshot-comparison instructions outside generic capability descriptions.
- A process-step suppression rule leaves the suppressed skill/tool/MCP in `AgentRuntimeContextManifest`.
- A management-only step cannot suppress a development skill without editing the agent template.
- Required capability denial does not fail the run or does not produce actionable diagnostics.
- Process template scope fields are stringly typed without validation wrappers/enums.
- Provider-level policies are exposed before provider identity can be selected reliably.

## Scope Boundaries

- This bundle does not require removing software-delivery process templates.
- This bundle does not require removing development agents.
- This bundle does require moving development-specific MAF wrapper behavior out of common workspace tools.
