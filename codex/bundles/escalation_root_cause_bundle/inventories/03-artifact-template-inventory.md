# Artifact Template Inventory

## Business Artifact Templates

| Artifact template | Source reference | Required audit |
| --- | --- | --- |
| Business plan | `repo://Templates/Processes/processes/business-plan-development/artifacts/business-plan.json` | Verify semantic completion gates and accepted artifact slot rules. |
| Financial model | `repo://Templates/Processes/processes/business-plan-development/artifacts/financial-model.json` | Verify assumptions, calculations, and artifact acceptance are not file-existence-only. |
| Go-to-market plan | `repo://Templates/Processes/processes/business-plan-development/artifacts/go-to-market-plan.json` | Verify proof and validation gates are typed or explicitly exempt. |
| Integrated business plan | `repo://Templates/Processes/processes/business-plan-development/artifacts/integrated-business-plan.json` | Verify multi-artifact composition cannot be accepted by structure alone. |
| Product assessment | `repo://Templates/Processes/processes/business-plan-development/artifacts/product-assessment.json` | Verify evidence-backed product assessment completion. |
| Strategy brief | `repo://Templates/Processes/processes/business-plan-development/artifacts/strategy-brief.json` | Verify semantic completion and acceptance wording. |

## Artifact Acceptance Rule

An artifact template is safe only if the runtime can distinguish:

- structured finalizer output is parseable;
- staged artifact exists;
- completion gates accepted it;
- produced artifact slot was promoted to parent/consumer context.

Physical existence of markdown, JSON, or generated files is not sufficient when a managed artifact slot or semantic process contract is required.
