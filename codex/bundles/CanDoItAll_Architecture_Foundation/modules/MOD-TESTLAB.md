# MOD-TESTLAB — TestLab: product test plans, tests and evidence

**Target owner:** TestLab

Product test definitions, cases, runs and evidence. TestLab is not repository build/test infrastructure and does not replace its QA. Tested objects and versions are explicit references.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.TestLab`

**Primary sources:** SRC-016, MAP-11

## Provided responsibilities

- Test plan/case/run/evidence queries and owner commands; verdicts with source revisions.
- Structure contributions, linked-test navigation and evidence export.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Projects/Structure source references; Agents, Workflows and Processes only for specifically supported runners.
- Storage for evidence bytes; Security for authorized access.

## Forbidden shortcuts

- A Test button opening only generic project TestLab does not prove a test of a specific node.
- Runtime exit success is neither evidence quality nor task acceptance.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: identify exactly which tests and runner operations are implemented.
- ESTIMATE: richer evidence attachments and evaluation of more artifact kinds.

## Persistence

TestLab owns test records and evidence metadata; Storage owns bytes. A historical verdict does not automatically apply to a new source version.

## Recorded capabilities

- **FEAT-094** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] TestLab concepts for validation and testing.
- **FEAT-095** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] TestPlan, TestCaseRecord, TestEvidenceRecord, TestRunRecord, and TestLabService.
- **FEAT-096** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] TestLab page, project links, and transfer participant.
- **FEAT-097** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Project Structure documentation treats test-plan forms and evidence attachments as follow-up work, not a completed universal Test action.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-003 — Governed agent execution:** caller.
- **CON-015 — Project lookup:** caller.
- **CON-021 — Ensure structure contribution:** caller.
- **CON-028 — Process run query/control:** caller.
- **CON-029 — Workflow catalog/version lookup:** caller.
- **CON-031 — Process run result publication:** caller.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-036 — TestLab plan/run/evidence:** declaration owner, implementation/integration boundary.
- **CON-043 — Managed storage lifecycle:** caller.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-054 — Agent run result publication:** caller.
- **CON-055 — Workflow run result publication:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-076 — TestLab evidence and verdict authoring:** declaration owner, implementation/integration boundary.

## Proof and unresolved questions

Relevant planned scenarios: QA-038, QA-074, QA-083, QA-094, QA-158.

Related gaps: GAP-023.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
