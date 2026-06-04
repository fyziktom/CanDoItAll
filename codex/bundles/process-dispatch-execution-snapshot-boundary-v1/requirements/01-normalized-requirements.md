# Normalized Requirements

| ID | Requirement | Owning subbundle(s) | Proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve MAF/product-tool provider decoupling from previous phases. | SB01, SB07, SB10, SB12 | MAF/Tooling product dependency scans and provider/policy tests. |
| RQ-002 | Do not start full Process Core extraction or driver-pack work. | SB01, SB03, SB07, SB10, SB12 | No-core/no-driver project scans. |
| RQ-003 | Inventory remaining AgentFramework execution model usage in process dispatch. | SB01, SB02 | Source inventory and direct type scan. |
| RQ-004 | Add process-owned execution result/detail/record/failure snapshot contracts without EF/UI/AgentFramework references. | SB02, SB03 | Contracts neutrality tests. |
| RQ-005 | Keep `ProcessAutomationExecutionClient` as the only adapter mapping AgentFramework execution runtime types into process snapshots. | SB04, SB07, SB12 | Source scan and client mapping tests. |
| RQ-006 | Migrate dispatcher start/detail/list consumers to process-owned snapshots. | SB05, SB07 | Direct AgentFramework type scan over dispatch partials. |
| RQ-007 | Normalize `AgentChatRunFailedException` and `AgentRunFailedException` inside the client boundary. | SB06, SB07 | Exception normalization unit/integration tests and dispatcher forbidden-exception scan. |
| RQ-008 | Isolate execution receipt and required-tool observation into a small helper that consumes process snapshots. | SB08, SB09 | Receipt helper tests and required-tool parity tests. |
| RQ-009 | Preserve artifact lineage, required-tool detection, provider metadata, and receipt semantics. | SB09, SB11, SB12 | Artifact-lineage and process runtime integration tests. |
| RQ-010 | Use refactor gates after every few implementation subbundles. | SB03, SB07, SB10, SB12 | Gate A/B/C/final reports. |
| RQ-011 | Do not perform small/medium/mobile viewport validation. | All | Large-screen policy scan and browser validation logs. |
| RQ-012 | Keep all changes source-backed, test-backed, and bundle-validator compliant. | All | Prepared/completed bundle validator proof. |
