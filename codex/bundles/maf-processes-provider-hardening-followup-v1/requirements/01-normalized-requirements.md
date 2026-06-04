# Normalized Requirements

| ID | Requirement | Observable acceptance | Owning subbundle(s) |
| --- | --- | --- | --- |
| RQ-001 | Preserve completed MAF -> Processes decoupling | MAF must not regain direct project/source dependency on CanDoItAll.Modules.Processes. | SB01, SB12 |
| RQ-002 | Clean branch hygiene before more runtime work | Classify and resolve unrelated codex/bundles churn before downstream work. | SB01 |
| RQ-003 | Harden runtime provider metadata | First-party providers must expose descriptor/ownership metadata. | SB02 |
| RQ-004 | Keep provider composition generic | MAF provider composition must use provider-neutral names and tests. | SB03 |
| RQ-005 | Providerize project-structure tools | Remove hard-coded project-structure attach method from MAF after parity proof. | SB04 |
| RQ-006 | Providerize image-generation tools | Remove hard-coded image-generation attach method from MAF after parity proof. | SB05 |
| RQ-007 | Stop for refactor checkpoint after product migrations | Review allowed MAF references and provider composition cleanliness before continuing. | SB06 |
| RQ-008 | Split process provider internally | ProcessAgentRuntimeToolProvider must be split without changing tool names/access behavior. | SB07 |
| RQ-009 | Use provider purpose/security context | Process provider must handle purpose/access policy explicitly. | SB08 |
| RQ-010 | Add provider observability | Provider ownership must be visible in diagnostics/proof and receipt tagging where feasible. | SB09 |
| RQ-011 | Refresh docs and guards | Docs must reflect providerized tool surface without overclaiming process core extraction. | SB10 |
| RQ-012 | Run integration smoke | Provider composition, process outbox, receipts, artifact lineage, and full build must pass. | SB11 |
| RQ-013 | Final red-team closure | No hidden coupling, parity loss, policy weakening, or process-core scope creep may remain. | SB12 |
