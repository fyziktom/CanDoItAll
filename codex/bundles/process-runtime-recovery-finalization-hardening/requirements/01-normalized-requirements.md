# Normalized Requirements

| ID | Requirement | Acceptance signal | Owner |
|---|---|---|---|
| R01 | Preserve a full map of process runtime, dispatcher, manager, driver, artifact, finalizer, and agent execution flows before implementation. | Current-state inventory cites exact source files, CodeAnalytics snapshot, and user-story/exception matrix. | SB01 |
| R02 | Represent connected process artifacts as concrete input packages, not only available slot ids. | Runtime can resolve producer step, source slot, artifact instance, content hash, storage/read ref, connection path, and required consumer step. | SB02 |
| R03 | Support artifacts produced by any connected prior step, including non-direct previous steps and subprocess boundary projections. | Tests cover direct, earlier non-direct, branch, and parent/child artifact flows. | SB02 |
| R04 | Expose fresh step contract retrieval to agents and finalizers. | Agent-facing tool or driver service returns step instructions, expected outputs, required artifacts, required tool receipts, branch choices, and manager handoff rules from durable runtime state. | SB03 |
| R05 | Keep context bounded. | Downstream steps receive artifact manifests, summaries, and retrieval handles by default; full product file dumps require explicit process-driver policy. | SB07 |
| R06 | Add a finalization gate before process advancement. | A step cannot complete unless required input artifacts were inspected or explicitly waived, required outputs were produced, required receipts are present, and branch/finalizer rules pass. | SB04 |
| R07 | Add manager-confirmed handoff for next-step readiness where required. | Manager or manager strategy confirms missing-input repair, access grant/reassignment, or downstream readiness before the runtime advances. | SB04 |
| R08 | Stop automatic same-step retry for missing upstream artifact/input. | Missing connected input classifies as upstream repair or manager action, not current-step safe retry. | SB05 |
| R09 | Stop automatic same-step retry for denied tools or missing access. | Denied or missing capability routes to manager access grant, reassignment, or terminal block with actionable state. | SB05 |
| R10 | Keep retry only for genuinely transient, idempotent, current-step failures. | Retry taxonomy distinguishes transient provider/runtime failure from missing input, missing artifact, denied capability, non-idempotent mutation, and instruction non-compliance. | SB05 |
| R11 | Keep generic runtime domain-neutral. | Runtime contracts mention process, step, artifact, finalization, manager, driver, retry, and handoff concepts only; .NET, browser, project-structure, and MAF details stay in drivers/templates/module integration. | SB06 |
| R12 | Use process drivers for domain-specific recovery/finalization policy. | Driver contracts own adapter-specific evidence and completion policy without adding runtime references to AgentFramework or MAF. | SB06 |
| R13 | Shrink or neutralize partial-class responsibility clusters. | Extracted services are independently unit-testable and source assertions prove old partial clusters no longer own moved behavior. | SB06 |
| R14 | Prove behavior with semantic and artifact-backed tests. | Critical subbundles produce failing-first, passing, source assertion, anti-stub, changed-file hash, and downstream smoke proof manifests during execution. | SB08 |
| R15 | Preserve all raw scope words and closure. | Execution report maps every raw architect note to solved, partially solved, or not solved with proof or blocker. | SB08 |

## Non-Goals

- Do not implement this bundle during preparation.
- Do not solve only the multi-team .NET development process. That process is an important regression scenario, not the generic architecture owner.
- Do not replace all process templates as a prerequisite for runtime hardening.
- Do not add fallback mechanisms that hide missing artifacts, missing rights, or missing proof.
