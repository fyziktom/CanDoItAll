# Production Example Workflows And LLM Tuning

## Status

- `Completed`

## Objective

- Seed practical workflow examples that exercise routing logic and production-oriented LLM/tool settings.

## Covered Inputs

- RQ-026 and RQ-027.

## Prerequisites

- Subbundle 06 datasource configuration present.
- Subbundle 07 decision creation semantics present for example authoring consistency.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components`
- `C:\repositories\CanDoItAll\tests`

## Deliverables

- Seed workflows for input document summary, email task extraction, email summary, email response drafting, XLSX read/write, internet fetch into project structure, and 5-10 additional useful production scenarios.
- Tuned LLM component instructions/settings with deterministic JSON outputs where routing requires structured decisions.
- Tests or scenario fixtures that prove examples exist and validate.

## Validation Depth

- Catalog/unit/integration proof plus scenario matrix coverage.

## Dependency Impact

- Workflow example seeding is opt-in through `Workflows:ExampleSeed` and does not mutate catalogs unless enabled.
- Seeded examples depend on the decision node and executor setup contracts from subbundle 07.
- LLM component settings use provider defaults without embedding secrets; runtime settings remain editable through the existing workflow settings service.

## Implementation Steps

1. Locate existing catalog seed/warmup patterns.
2. Add examples through the established seed path.
3. Use structured JSON output for LLM routing decisions.
4. Add explicit route metadata for IF/ELSE, SWITCH/default, and fan-out.
5. Validate seeded definitions and runtime policy.

## Do Not Do

- Do not hard-code provider secrets.
- Do not seed examples that require unavailable external credentials to validate structure.

## Acceptance Checklist

- At least 13 practical workflows are present.
- Required document, email, XLSX, and internet/project-structure examples are included.
- Examples use tuned prompts/settings and route metadata instead of vague generic prompts.

## Proof Required

- Targeted tests showing example inventory and validation.
- Scenario matrix output.

## Closure Proof

- Added opt-in workflow example seeding with 15 practical definitions and 15 matching LLM components.
- Required examples are present for document summary, email task creation, email response, XLSX read/write, internet fetch, and project-structure storage.
- Additional examples cover support SLA, sales lead routing, release readiness, incident fan-out, HR staffing, contract renewal, customer feedback fan-out, vendor risk, and meeting notes.
- LLM component instructions require structured JSON, lowercase branch tokens, explicit projectId preservation, and literal predicate data for IF/SWITCH routing.
- Evidence: `reviews/evidence/subbundle-08/workflow-example-inventory.txt` and `reviews/evidence/subbundle-08/llm-component-settings.txt`.

## Browser Validation Logging

- Browser inventory screenshot if examples are visible in the workflow list.

## Progression Gate

- Subbundle 09 may execute observations after examples are seeded and structurally valid.

## Suggested Agent Prompt

```text
Implement subbundle 08 only: seed production-like workflow examples and tune LLM/workflow settings. Validate inventory and route metadata.
```
