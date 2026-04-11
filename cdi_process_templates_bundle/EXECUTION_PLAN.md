# Execution plan

The bundle must be executed in the listed order. Downstream work is blocked whenever an architecture review or validation gate fails.

## 01-schema-and-pack-refresh — Schema and pack refresh

**Purpose:** Refresh the template-pack schema so it models role usages, dependencies, artifact inputs, branch coordinates, and shared/local resources explicitly.

**Depends on:** None

**Progression gate:** Must pass architecture review gate A before the next subbundle may begin.

**Deliverables:**
- Updated definition schema and manifest
- Rebuilt process-template pack folders
- Updated workbook tabs for dependencies and artifact inputs

**Corrective rule:** Create a corrective subbundle focused on schema mismatches, then rerun gate A.

## 02-baseline-scenario-realignment — Baseline scenario realignment

**Purpose:** Realign the seeded baseline scenarios to the current five-process repository expectations and current step keys.

**Depends on:** 01-schema-and-pack-refresh

**Progression gate:** Validator must confirm five expected baseline scenarios and exact key process expectations.

**Deliverables:**
- Five current baseline scenarios
- Updated validation assertions for software, branching, and hotfix expectations

**Corrective rule:** Create a corrective subbundle for baseline drift, then rerun validation.

## 03-process-template-enhancement — Process template enhancement

**Purpose:** Revisit all processes and remove earlier simplifications that were only present because the older module lacked current features.

**Depends on:** 02-baseline-scenario-realignment

**Progression gate:** Each process must have detailed roles, artifacts, checklists, validations, prompts, dependencies, and artifact inputs where relevant.

**Deliverables:**
- Nine updated process definitions
- Detailed role sidecars
- Step-level markdown docs

**Corrective rule:** Create a process-specific corrective subbundle and do not continue until the process passes review.

## 04-mermaid-and-sidecar-driver — Mermaid and sidecar driver

**Purpose:** Ensure every template can export Mermaid flowchart and sequence diagrams plus supporting markdown sidecars.

**Depends on:** 03-process-template-enhancement

**Progression gate:** Mermaid exporter and supporting file inventory must cover definition docs and resource docs.

**Deliverables:**
- Mermaid flowcharts
- Mermaid sequence diagrams
- Supporting sidecar file inventory

**Corrective rule:** Create an exporter corrective subbundle and rerun architecture review gate B.

## 05-runtime-projection-and-import-parity — Runtime projection and import parity

**Purpose:** Project current-module import envelopes from the pack and verify parity for dependencies, artifact inputs, and decision roles.

**Depends on:** 04-mermaid-and-sidecar-driver

**Progression gate:** Projection compatibility reports must show no blocking issues.

**Deliverables:**
- Current-module import envelopes
- Compatibility reports
- Projection parity tests

**Corrective rule:** Create a projection corrective subbundle before continuing.

## 06-tests-and-regression-net — Tests and regression net

**Purpose:** Strengthen unit tests and validation scripts so future process-module changes surface pack regressions immediately.

**Depends on:** 05-runtime-projection-and-import-parity

**Progression gate:** Test inventory must cover loader, projection, exporter, catalog, and current baseline expectations.

**Deliverables:**
- Updated xUnit tests
- Validation script
- Regression expectations for current architecture

**Corrective rule:** Create a test-gap corrective subbundle and rerun gate B.

## 07-architecture-review-gate-a — Architecture review gate A

**Purpose:** Stop after the early pack refresh and re-check schema fidelity before more implementation effort is spent.

**Depends on:** 01-schema-and-pack-refresh,02-baseline-scenario-realignment,03-process-template-enhancement

**Progression gate:** Must pass or produce a corrective subbundle.

**Deliverables:**
- Architecture review memo A
- Gap register
- Go/no-go decision

**Corrective rule:** Open a corrective subbundle immediately and prohibit downstream progress.

## 08-corrective-canvas-chrome-dehardcode — Corrective subbundle — canvas chrome de-hardcode

**Purpose:** Address the remaining hardcoded authoring chrome in ProcessCanvasSurfaceFactory so quick-create and group actions become file-driven.

**Depends on:** 07-architecture-review-gate-a

**Progression gate:** Either complete the de-hardcode patch or explicitly accept the debt in the final QA memo.

**Deliverables:**
- Patch plan and sample patch
- Chrome-actions catalog sidecar
- Acceptance criteria for UI de-hardcoding

**Corrective rule:** Escalate as architectural debt that must stay visible in the final bundle.

## 09-architecture-review-gate-b — Architecture review gate B

**Purpose:** Stop again after projection, Mermaid, and tests to catch wrong architectural direction before final closure.

**Depends on:** 04-mermaid-and-sidecar-driver,05-runtime-projection-and-import-parity,06-tests-and-regression-net,08-corrective-canvas-chrome-dehardcode

**Progression gate:** Must pass or produce another corrective subbundle.

**Deliverables:**
- Architecture review memo B
- Corrective action closure evidence
- Updated traceability matrix

**Corrective rule:** Open a corrective subbundle before any final QA activity.

## 10-final-qa-audit-and-closure — Final QA audit and closure

**Purpose:** Perform the strict final QA and senior architect inspection, then package the final ZIP only after all gates are satisfied.

**Depends on:** 09-architecture-review-gate-b

**Progression gate:** Final QA memo must state whether any debt remains and why it is or is not acceptable.

**Deliverables:**
- Final QA memo
- Validation result
- Bundle index and final ZIP

**Corrective rule:** Create one more corrective subbundle and repeat the final audit.
