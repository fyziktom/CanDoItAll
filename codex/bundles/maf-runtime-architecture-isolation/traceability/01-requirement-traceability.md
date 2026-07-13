# Requirement Traceability

## Raw Note Closure

| Raw Note | Bundle Location | Owning Subbundle | Planned Proof | Notes |
| --- | --- | --- | --- | --- |
| M001 Defer Financial Strategist until after MAF refactor. | `inputs/00-original-request.md`, `requirements/01-normalized-requirements.md` | SB01 | Scope audit showing Financial Strategist work removed. | Future case only. |
| M002 MAF runtime is the main trouble. | `analysis/01-current-state.md` | SB01, SB07 | Responsibility map and closure report. | Generic runtime focus. |
| M003 Huge partial class is not real isolation. | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | SB01-SB05 | Source assertions and boundary tests. | Partial movement alone is rejected. |
| M004 Hard to unit test. | `analysis/01-current-state.md` | SB06 | Direct collaborator tests and reflection-reduction report. | Existing reflection tests cited. |
| M005 Current shape causes recurring trouble. | `architecture/01-target-solution.md` | SB02-SB07 | Behavior parity plus stable extension points. | Stable base for future agent improvements. |
| M006 Remove margin/domain-specific things. | `requirements/01-normalized-requirements.md` | SB01 | Bundle grep/scope audit. | No margin/document/writeback subbundle. |
| M007 Isolate drivers, strategies, helpers. | `architecture/01-target-solution.md` | SB02-SB05 | Extracted collaborators and tests. | Real collaborator boundaries required. |
| M008 Analyze performance effect. | `analysis/01-current-state.md` | SB01, SB07 | Baseline and after-change performance report. | Local runtime cost separated from provider latency. |
| M009 Improve mocking for integration tests. | `requirements/01-normalized-requirements.md` | SB06 | Fake provider/tool/context/workspace/MCP harness proof. | Reduce need for full runtime construction. |
| M010 Stable base for future cases. | `README.md`, `plan/01-phase-plan.md` | SB07 | Final architecture closure and follow-up readiness. | Agent-specific cases resume later. |
| M011 Do not implement. | `README.md` | Preparation | Prepared validator only. | This turn performs bundle repair only. |

## Requirement Traceability

| Requirement | Owning Subbundle | Planned Proof | Closure Dependency |
| --- | --- | --- | --- |
| R001 | SB01 | Scope audit and bundle grep. | Must complete before all implementation work. |
| R002 | SB01 | Responsibility map artifact. | Unlocks SB02. |
| R003 | SB02 | Contract review, registration tests, dependency classification. | Unlocks SB03-SB05. |
| R004 | SB03 | Direct tests and parity tests for capability/tool-provider composition. | Feeds SB06/SB07. |
| R005 | SB04 | Direct tests and parity tests for provider/session/finalizer drivers. | Feeds SB06/SB07. |
| R006 | SB05 | Direct tests and parity tests for feature drivers. | Feeds SB06/SB07. |
| R007 | SB02, SB04, SB05 | Missing-service negative tests and registration tests. | Must hold across extracted seams. |
| R008 | SB06 | Fake dependency harness and integration mock tests. | Required for closure. |
| R009 | SB06 | Reflection-reduction report and remaining-reflection justification. | Required for closure. |
| R010 | SB03-SB07 | Existing tests plus new parity tests. | Required for every extraction. |
| R011 | SB01, SB07 | Baseline and after-change performance measurements. | Required before closure. |
| R012 | All | `proof/SBxx` manifests, semantic invariants, execution report updates. | Final closure gate. |
