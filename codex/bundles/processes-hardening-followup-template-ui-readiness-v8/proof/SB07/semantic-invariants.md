# SB07 Semantic Invariants

- Invariant ID: SB07-INV-001
- Source raw note: F05 project-structure writeback tool classification.
- Expected behavior: Project-structure tools are explicitly classified in policy metadata; read-only project-structure tools remain read operations, mutation project-structure tools require `ExecuteExternalAction`, unknown `project_structure_*` tools are denied through `Unknown` classification, and templates that call project-structure writeback tools declare `ExternalActionControlled` contracts.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first.txt` shows `project_structure_node_create` previously bypassed explicit mutation classification.
- Passing test: `bundle://proof/SB07/transcripts/passing.txt` covers unit policy denial/allowance and integration template contract projection.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`, `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`, and screenshot/layout template definitions listed in `bundle://proof/SB07/transcripts/changed-file-hashes.txt`.
- Production assertions: mutation project-structure tools require `ExecuteExternalAction`; unknown `project_structure_*` tools classify as `Unknown`; read-only project-structure discovery remains read-only.
- Red-team negative case: a writeback tool without `ExecuteExternalAction` is denied instead of inheriting generic read behavior.
- Downstream dependency check: SB08 and SB13 can rely on explicit project-structure tool policy when migrating and documenting template contracts.
- Required proof: failing-first/adversarial proof, passing production-path unit/integration tests, source assertions, anti-stub audit, changed-file hashes.
