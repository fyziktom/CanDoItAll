# Requirement Traceability

| Requirement | Source input | Bundle destination | Owning subbundle | Proof expectation |
| --- | --- | --- | --- | --- |
| `R001` Preserve all functionality | `bundle://inputs/00-original-request.md` | `bundle://requirements/01-normalized-requirements.md` | `SB07` | Existing and focused tests pass; execution report cites proof manifests. |
| `R002` Split integration file | `bundle://inputs/00-original-request.md` | `bundle://analysis/01-current-state.md` | `SB01`, `SB02`, `SB03`, `SB04`, `SB06` | File-size/coupling audit plus direct tests for extracted services. |
| `R003` Keep runtime domain-neutral | `bundle://inputs/00-original-request.md` | `bundle://architecture/01-target-solution.md` | `SB01`, `SB03`, `SB05` | Non-software process prompt/launch tests prove absence of .NET/software-delivery guidance. |
| `R004` Isolate AgentFramework driver behavior | `bundle://inputs/00-original-request.md` | `bundle://architecture/01-target-solution.md` | `SB02` | Driver service tests and adapter behavior regression proof. |
| `R005` Extract prompt strategies | `bundle://inputs/00-original-request.md` | `bundle://architecture/01-target-solution.md` | `SB03` | Driver prompt strategy tests, model/provider seam proof, and no Processes-to-MAF dependency scan. |
| `R006` Isolate subprocess behavior | `bundle://analysis/01-current-state.md` | `bundle://plan/01-phase-plan.md` | `SB02` | Active, stopped, blocked, relaunched, and coordinator-failure child-run tests. |
| `R007` Extract completion evidence policies | `bundle://analysis/01-current-state.md` | `bundle://plan/01-phase-plan.md` | `SB04` | Driver-owned product path, receipt, content, grounding, and materialization policy tests plus dependency scan. |
| `R008` Domain-specific launch enrichment | `bundle://inputs/00-original-request.md` | `bundle://architecture/01-target-solution.md` | `SB05` | .NET contributor tests plus unrelated enterprise process negative tests. |
| `R009` Dispatcher cleanup | `bundle://analysis/01-current-state.md` | `bundle://plan/01-phase-plan.md` | `SB06` | Branch router injection, stale method removal, fake selected-driver dispatch replacement, and claim/recovery regression proof. |
| `R010` Explicit diagnostics | `bundle://inputs/00-original-request.md` | `bundle://requirements/01-normalized-requirements.md` | `SB02`, `SB04`, `SB06` | Tests assert diagnostic codes and safe summaries with run/step/tool context. |
| `R011` Decompose tests | `bundle://analysis/01-current-state.md` | `bundle://plan/01-phase-plan.md` | `SB07` | Test inventory shows behavior moved from monolithic tests to focused suites. |
| `R012` Enterprise-domain flexibility | `bundle://inputs/00-original-request.md` | `bundle://requirements/01-normalized-requirements.md` | `SB03`, `SB05`, `SB07` | Business/supplier/reporting/quality scenarios included in prompt and launch proof. |
| `R013` Driver-owned prompt/evidence/dispatch behavior | `bundle://inputs/03-architecture-followup-request.md` | `bundle://architecture/02-driver-boundary-and-dependency-rules.md` | `SB01`, `SB02`, `SB03`, `SB04`, `SB06` | Driver port source assertions and tests prove generic dispatcher delegates driver-specific behavior. |
| `R014` No Processes to MAF dependency | `bundle://inputs/03-architecture-followup-request.md` | `bundle://inventories/02-dependency-direction-inventory.md` | `SB01`, `SB07` | Project-reference and namespace scans fail on Processes-to-MAF references. |
| `R015` Separate generic dispatch orchestration from step execution dispatch | `bundle://inputs/03-architecture-followup-request.md` | `bundle://plan/01-phase-plan.md` | `SB01`, `SB06`, `SB07` | Tests and source assertions show scheduling/claims remain generic while selected driver handles step execution policy. |
