# Artifact Template Inventory

## Artifact Template Files

The repo has process artifact/checklist/validation templates under multiple process families:

- `repo://Templates/Processes/processes/ai-assisted-change-delivery/artifacts/evaluation-benchmark-report.json`
- `repo://Templates/Processes/processes/ai-assisted-change-delivery/artifacts/execution-trace-pack.json`
- `repo://Templates/Processes/processes/architecture-decision-governance/artifacts/decision-brief.json`
- `repo://Templates/Processes/processes/branching-code-review/artifacts/architecture-escalation-brief.json`
- `repo://Templates/Processes/processes/branching-code-review/artifacts/qa-lane-validation-note.json`
- `repo://Templates/Processes/processes/branching-code-review/artifacts/repair-brief.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/business-plan.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/financial-model.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/go-to-market-plan.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/integrated-business-plan.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/product-assessment.json`
- `repo://Templates/Processes/processes/business-plan-development/artifacts/strategy-brief.json`
- `repo://Templates/Processes/processes/customer-onboarding/artifacts/configuration-baseline.json`
- `repo://Templates/Processes/processes/customer-onboarding/artifacts/customer-onboarding-brief.json`
- `repo://Templates/Processes/processes/hotfix-rollout/artifacts/rollback-trigger-sheet.json`
- `repo://Templates/Processes/processes/incident-response/artifacts/containment-decision-note.json`
- `repo://Templates/Processes/processes/incident-response/artifacts/containment-plan.json`
- `repo://Templates/Processes/processes/oss-intake-supply-chain-governance/artifacts/license-obligation-matrix.json`
- `repo://Templates/Processes/processes/release-readiness-and-deployment/artifacts/cutover-watch-roster.json`
- `repo://Templates/Processes/processes/software-delivery/artifacts/migration-rehearsal-pack.json`

## Migration Scope

Most artifact templates do not own runtime branch routing. Do not edit business/customer/incident/OSS/release artifacts unless execution finds they are used as acceptance criteria or repair target carriers in impacted processes.

Artifact work required by this bundle:

- add or update a reusable acceptance criteria matrix artifact/template if none exists;
- ensure QA, repair, and recheck artifacts can cite acceptance criteria ids and runtime gate findings;
- ensure branch-routed gate findings can be persisted as a managed artifact or appended section;
- ensure template migration does not store native absolute product paths as durable proof;
- ensure artifact recovery does not infer accepted/repair branch outcomes from weak status-only text.

## Implementation Closure

- Acceptance criteria are represented by `ProcessAcceptanceCriteriaMatrix` in `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessAcceptanceCriteriaModels.cs` and emitted as launch metadata by Workbench contributors.
- Runtime gate findings are appended by the adapter when a routed live path needs repair evidence; no unrelated business/customer/incident artifact template was edited.
- QA/recheck evidence now cites criteria ids and runtime gate diagnostics through process metadata and managed artifacts, preserving grounded aliases rather than native absolute product paths.
- Artifact templates outside software/Blazor process delivery were left unchanged because the audit found no accepted/repair browser proof failure mode there.

## Artifact Acceptance Questions

- Which artifact owns the original project-structure acceptance criteria?
- Which artifact records runtime gate findings when accepted output is routed to repair?
- Which artifact maps implementation changes and tests to criteria ids?
- Which artifact is read by repair and recheck after a branch-routed gate failure?
- Are native absolute paths replaced by grounded aliases or product-root-relative references?
- Can artifact recovery preserve branch outcome keys without accepting contradictory or status-only artifacts?
