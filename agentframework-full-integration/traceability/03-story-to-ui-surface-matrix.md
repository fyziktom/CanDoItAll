# 03 — Story To UI Surface Matrix

| Story ID | Actor ID | Primary UI / proof surface | Owning subbundle | Validation expectation |
| --- | --- | --- | --- | --- |
| US-01 | ACT-01 | /agents?tab=Providers | 04-provider-ownership-bridge-and-legacy-runtime-retirement | Must be browser-proofed |
| US-02 | ACT-02 | /agents?tab=Agents | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience | Must be browser-proofed |
| US-03 | ACT-01 | shell main nav + /agents | 01-foundation-import-map-and-module-skeleton | Must be browser-proofed |
| US-04 | ACT-13 | /collaboration?tab=Inbox | 02-collaboration-domain-notification-and-conversation-foundation | Must be browser-proofed |
| US-05 | ACT-10 | /collaboration/thread/{id} | 02-collaboration-domain-notification-and-conversation-foundation | Must be browser-proofed |
| US-06 | ACT-10 | /processes designer canvas + runtime transcript | 03-process-messaging-policy-canvas-and-runtime-enforcement | Must be browser-proofed |
| US-07 | ACT-12 | /processes run detail | 09-agent-execution-orchestration-artifact-bridge-and-run-observability | Must be browser-proofed |
| US-08 | ACT-03 | /crm-hr/agents + project resource pickers | 06-crmhr-resource-binding-and-agent-management-surface | Must be browser-proofed |
| US-09 | ACT-03 | /crm-hr/agents | 06-crmhr-resource-binding-and-agent-management-surface | Must be browser-proofed |
| US-10 | ACT-09 | /processes launch wizard | 07-process-launch-planning-hr-recommendation-and-default-strategies | Must be browser-proofed |
| US-11 | ACT-04 | /processes launch wizard | 07-process-launch-planning-hr-recommendation-and-default-strategies | Must be browser-proofed |
| US-12 | ACT-07 | /processes definition editor | 07-process-launch-planning-hr-recommendation-and-default-strategies | Must be browser-proofed |
| US-13 | ACT-07 | /processes canvas | 03-process-messaging-policy-canvas-and-runtime-enforcement | Must be browser-proofed |
| US-14 | ACT-06 | /collaboration approvals or /processes launch detail | 08-manager-approval-human-substitution-and-resource-provisioning | Must be browser-proofed |
| US-15 | ACT-08 | /processes launch wizard | 07-process-launch-planning-hr-recommendation-and-default-strategies | Must be browser-proofed |
| US-16 | ACT-09 | /projects assignments + /processes launch detail | 08-manager-approval-human-substitution-and-resource-provisioning | Must be browser-proofed |
| US-17 | ACT-03 | /processes launch wizard (no-provider fallback state) | 07-process-launch-planning-hr-recommendation-and-default-strategies | Must be browser-proofed |
| US-18 | ACT-10 | /agents?tab=Governance or Diagnostics | 05-agent-catalog-persistence-workspace-scoping-and-governance-bridges | Must be browser-proofed |
| US-19 | ACT-12 | /processes run detail -> artifacts | 09-agent-execution-orchestration-artifact-bridge-and-run-observability | Must be browser-proofed |
| US-20 | ACT-02 | /agents?tab=Chat and Governance | 05-agent-catalog-persistence-workspace-scoping-and-governance-bridges | Must be browser-proofed |
| US-21 | ACT-08 | deep links between /processes, /crm-hr/agents, /agents | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience | Must be browser-proofed |
| US-22 | ACT-01 | /agents?tab=Providers and /settings redirect | 10-agent-ui-recomposition-shell-tabs-and-cross-module-experience | Must be browser-proofed |
| US-23 | ACT-15 | /agents?tab=Scenarios | 11-scenario-migration-real-e2e-validation-and-playwright-proof | Must be browser-proofed |
| US-24 | ACT-15 | /processes launch + /agents scenarios + run detail | 11-scenario-migration-real-e2e-validation-and-playwright-proof | Must be browser-proofed |
| US-25 | ACT-12 | execution report + scenario proof surfaces | 11-scenario-migration-real-e2e-validation-and-playwright-proof | Must be browser-proofed |
| US-26 | ACT-01 | bundle plan and execution report | 12-data-backfill-cleanup-refactor-gates-and-final-closure | Document/proof surface |
| US-27 | ACT-12 | story workbook + execution report | 12-data-backfill-cleanup-refactor-gates-and-final-closure | Must be browser-proofed |
| US-28 | ACT-01 | bundle self-review + final report | 12-data-backfill-cleanup-refactor-gates-and-final-closure | Document/proof surface |

## Closure Rule

Pokud během implementace vyjde najevo, že některá story nemá realistický UI surface nebo route, executor ji nesmí jen označit jako „backend-only“. Musí:
1. aktualizovat matrix,
2. vytvořit nebo rozšířit příslušný UI subbundle,
3. teprve potom pokračovat.
