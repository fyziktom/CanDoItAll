# MOD-SCHEDULER — SchedulerPlanner: execution calendar, not a task plan

**Target owner:** SchedulerPlanner

Schedules, time zone/cron, firing identity, dispatch and schedule history. Quartz is an execution projection of Scheduler. Target workflow/process state belongs to its runtime, not Scheduler.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner`

**Primary sources:** SRC-013, SRC-003, MAP-11

## Provided responsibilities

- Schedule CRUD and state; explicit suspension, deletion and reconciliation.
- Workflow-scheduling tool adapter and firing correlation.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Workflows/Processes: typed launch and run state; CRM, Projects and Structure: input-option providers.
- Agents: managed Scheduler agent lookup/launch; Security: scope.

## Forbidden shortcuts

- Do not confuse Gantt planning with cron triggers.
- No second process engine or direct reads of foreign workflow EF rows.
- An enum or UI item alone does not establish target support.

## Future needs and explicit limits

- REQUIRES VERIFICATION: older README and analysis disagree about process scheduling. The revision audit confirms the managed Scheduler tool provider exposes workflow scheduling only; this does not certify all other scheduler surfaces.
- REQUIRED FOR THE BOUNDARY: missed fires, DST, retry and disabling during dispatch must not duplicate launch.
- ESTIMATE: additional target support is a separate feature.

## Persistence

SchedulerPlanner_Plans/Runs and Quartz state retain one business authority in the plan and a reconciliation adapter in Quartz. Firing keys remain unique through retries and restarts. Avoid competing raw-SQL schema definitions.

## Recorded capabilities

- **FEAT-065** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Scheduler plans, run history, cron, and Quartz/scheduler integration.
- **FEAT-066** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Dispatcher deduplication and lifecycle transitions.
- **FEAT-067** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Managed Scheduler agent discovers workflow targets and schedules and creates workflow schedules.
- **FEAT-068** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workflow versions, schemas, and options integrate CRM, Projects, Structure, and connections.
- **FEAT-069** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workflow launching and process-slot discovery do not demonstrate complete target parity.
- **FEAT-116** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] Managed Scheduler tools are interactive and workflow-only: discover targets/schedules and create a schedule.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** caller.
- **CON-010 — CRM party/resource catalog:** caller.
- **CON-015 — Project lookup:** caller.
- **CON-027 — Process launch:** caller.
- **CON-028 — Process run query/control:** caller.
- **CON-029 — Workflow catalog/version lookup:** caller.
- **CON-030 — Workflow launch/control:** caller.
- **CON-037 — Schedule plan and fire dispatch:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-066 — Scheduler target and schedule discovery:** declaration owner, implementation/integration boundary.
- **CON-067 — Governed workflow schedule administration:** declaration owner, implementation/integration boundary.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-034, QA-062, QA-063, QA-064, QA-147, QA-148, QA-162.

Related gaps: GAP-014, GAP-016, GAP-017, GAP-036.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
