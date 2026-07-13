# SB06 Semantic Invariants

## Invariant MAF-SB06-ARCHITECTURE-CLOSURE

- Invariant ID: `MAF-SB06-ARCHITECTURE-CLOSURE`
- Source raw note: the refactor must be validated together after MAF preparation, process connection, and architecture isolation.
- Expected behavior: focused tests, full unit tests, filtered integration tests, isolated builds, JSON parse checks, text scans, dependency scans, and CodeAnalytics all pass without reintroducing domain leaks or process-to-MAF-wrapper coupling.
- Disallowed shallow implementation: marking subbundles complete using only prose proof, or validating MAF and process changes independently without an end-to-end scoped process template.
- Failing-first test: `bundle://proof/SB06/transcripts/adversarial-negative.txt` proves the final process-to-MAF wrapper dependency scan remains empty.
- Passing test: `ProjectStructureAgentIntegrationTests` filtered integration run in `bundle://proof/SB06/transcripts/passing.txt`.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs` with hash `B61481DA80712722BB958CC036C9F06E896D6FAE9F561910931FC1A5CC58311E`.
- Production assertions: the architecture gate records no cycles, no blocking CodeAnalytics errors, no MAF wrapper reference from process modules, and no common MAF development image prompt leakage.
- Red-team negative case: a process module reference to `CanDoItAll.AgentFramework.Maf` or common MAF development image prompt text would fail closure scans.
- Downstream dependency check: SB01 through SB05 are completed in dependency order and SB06 verifies them together with unit, integration, build, scan, and analytics proof.
