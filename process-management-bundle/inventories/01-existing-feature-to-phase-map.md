# Existing Feature To Phase Map

| Legacy feature | Legacy focus | New execution owner |
| --- | --- | --- |
| `PRM-F01` | Process module foundation and shell integration | `subbundles/04-process-module-shell-and-storage-foundation` |
| `PRM-F02` | Process definition language and versioning | `subbundles/05-process-definition-lifecycle-and-governance-model` |
| `PRM-F03` | Actor roles and CRM-HR bindings | `subbundles/01-canonical-ownership-and-cross-repo-convergence`, `subbundles/06-role-templates-contracts-and-staffing-authoring` |
| `PRM-F04` | Step contracts, inputs, outputs, and evidence | `subbundles/06-role-templates-contracts-and-staffing-authoring`, `subbundles/10-work-briefs-decision-records-and-artifact-trust` |
| `PRM-F05` | Transition rules and explicit handoffs | `subbundles/09-runtime-state-machine-approvals-and-decision-rights` |
| `PRM-F06` | Approval policies, escalations, governance gates | `subbundles/09-runtime-state-machine-approvals-and-decision-rights` |
| `PRM-F07` | Runtime execution state machine and assignments | `subbundles/09-runtime-state-machine-approvals-and-decision-rights` |
| `PRM-F08` | Execution timeline, audit journal, and replay | `subbundles/11-journal-forensics-operating-modes-and-import-export` |
| `PRM-F09` | Canvas modeler and interactive diagrams | `subbundles/07-canvas-authoring-and-component-first-ui-foundation` |
| `PRM-F10` | Project, Workbench, and shell projections | `subbundles/13-project-activity-validation-and-process-projections` |
| `PRM-F11` | Activity, Automation, Validation, and TestLab hooks | `subbundles/13-project-activity-validation-and-process-projections` |
| `PRM-F12` | Import/export, Mermaid, and template seeding | `subbundles/11-journal-forensics-operating-modes-and-import-export` |
| `PRM-F13` | Future AgentFramework adapter and AI executor seam | `subbundles/14-agentframework-bridge-and-registry-convergence` |
| `PRM-F14` | Operational intelligence and improvement backlog | `subbundles/18-conformance-learning-and-improvement-loop` |
| `PRM-F15` | Storage, migrations, and performance hardening | `subbundles/04-process-module-shell-and-storage-foundation` |
| `PRM-F16` | Role and agent templates, staffing briefs, sourcing handoffs | `subbundles/06-role-templates-contracts-and-staffing-authoring` |
| `PRM-F17` | Process ownership, interfaces, customer, and value alignment | `subbundles/05-process-definition-lifecycle-and-governance-model` |
| `PRM-F18` | Variants, exceptions, input quality, and decision rights | `subbundles/09-runtime-state-machine-approvals-and-decision-rights` |
| `PRM-F19` | Outcome metrics, capacity, and wait-state telemetry | `subbundles/17-metrics-economics-capability-gaps-and-decision-intelligence` |
| `PRM-F20` | Change governance, prioritization, literacy, management adoption | `subbundles/15-live-runtime-canvas-and-management-governance-ux` |
| `PRM-F21` | Conformance, field observation, and reality alignment | `subbundles/18-conformance-learning-and-improvement-loop` |
| `PRM-F22` | Process-native work briefs, baton handoffs, governed triage | `subbundles/10-work-briefs-decision-records-and-artifact-trust` |
| `PRM-F23` | AgentFramework convergence and shared registries | `subbundles/14-agentframework-bridge-and-registry-convergence` |
| `PRM-F24` | Live process execution canvas overlays and baton visibility | `subbundles/15-live-runtime-canvas-and-management-governance-ux` |

## Additional Coverage Added By This Repair Pass

- `subbundles/01-canonical-ownership-and-cross-repo-convergence`
  adds an explicit pre-implementation source-of-truth hardening step that the legacy pack did not separate.
- `subbundles/02-development-seed-packs-and-scenario-baseline`
  adds an explicit seed baseline for integration tests, demos, Playwright flows, and later repair bundles.
- `subbundles/10-work-briefs-decision-records-and-artifact-trust`
  broadens the legacy work-brief feature so explainability and artifact-trust metadata are not left as vague future notes.
- `subbundles/11-journal-forensics-operating-modes-and-import-export`
  broadens the journal and import/export scope to reserve forensic replay and operating-mode context.
- `subbundles/17-metrics-economics-capability-gaps-and-decision-intelligence`
  broadens legacy metrics so decision-quality and execution-cost analysis have a planned home.
