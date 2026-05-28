# SB10 Semantic Invariants

## Invariants

- Invariant ID: SB10-INV-001
- Source raw note: RN10 - Ensure agents have needed skills/tools and do not improvise process operations.
- Expected behavior: Required process role capabilities are represented as typed `AgentCapabilityRequirement` records and evaluated before dispatch by `AgentCapabilityRequirementEvaluator`.
- Disallowed shallow implementation: Prompt-only wording, docs-only governance, source-only proof for runtime behavior, UI-only hiding of errors, a no-op evaluator that always returns success, or hardcoded project/run/Tetris/Blazor special cases.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt proves missing and retired required skills produce typed diagnostics and that a no-op empty evaluation body is absent.
- Passing test: bundle://proof/SB10/transcripts/passing.txt proves all 6 focused capability filtering tests pass.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Models/Capabilities/CapabilityModels.cs; repo://src/CanDoItAll.AgentFramework.Core/Capabilities/AgentCapabilityRequirementEvaluator.cs; repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs; repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs.
- Production assertions: `AgentCapabilityRequirementEvaluator` emits `AgentCapabilityDiagnostic` records with code, severity, agent identity, role key, capability kind, capability key, and message for missing assignment, missing catalog item, stale assignment, and retired capability.
- Red-team negative case: A role with a missing `candoitall-api-processes` skill or a retired workspace delivery skill cannot pass requirement evaluation.
- Downstream dependency check: SB11, SB12, and SB18 can rely on typed missing capability diagnostics and role matrix guidance before API parity, docs refresh, and final red-team closure.

- Invariant ID: SB10-INV-002
- Source raw note: RN10 - Ensure agents do not improvise process operations when process tools are missing or unclassified.
- Expected behavior: Agents must not use tools that are absent from their composed capability set or present without policy classification.
- Disallowed shallow implementation: Treating unknown read-like tools as safe, allowing unclassified process mutations, or moving the decision into prompt text.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt proves `DefaultAgentToolInvocationPolicy` denies unknown tools and known tools with no registered classification.
- Passing test: bundle://proof/SB10/transcripts/passing.txt proves all 117 `AgentToolInvocationPolicyTests` pass.
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs remains the policy regression source; no production policy change was needed for this invariant.
- Production assertions: `DefaultAgentToolInvocationPolicy` returns `Deny` with reasons containing `not part of the composed capability set` and `no registered invocation policy classification`.
- Red-team negative case: An agent cannot call a made-up read-like tool or unregistered process mutation tool and proceed as if it were governed.
- Downstream dependency check: SB11 and SB12 can document process API and tool parity without weakening policy classification.

- Invariant ID: SB10-INV-003
- Source raw note: RN10 - Provide a role-by-role skill/tool matrix for process agents.
- Expected behavior: Process author, manager, step executor, reviewer/QA, and template curator roles have explicit skill/tool matrix guidance in AgentFramework Core docs and the active `candoitall-api-processes` skill.
- Disallowed shallow implementation: Updating only repository docs while the active Codex skill remains stale, or documenting process roles without runtime capability diagnostics.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt and bundle://proof/SB10/transcripts/source-assertions.txt prove hardcoded project/run/Tetris paths are absent from runtime source and no no-op evaluator is present.
- Passing test: bundle://proof/SB10/transcripts/source-assertions.txt proves both role matrices and anti-improvisation policy references exist.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Core/README.md; repo://codex/skills/candoitall-api-processes/SKILL.md.
- Production assertions: `bundle://proof/SB10/transcripts/skill-sync.txt` proves the repo skill and active Codex skill copy have matching SHA-256 hashes.
- Red-team negative case: Future process docs or automation cannot claim SB10 closure if the active skill copy diverges or omits the matrix.
- Downstream dependency check: SB12 docs/skills refresh and SB18 final governance can cite the synced skill matrix as current.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| `AgentCapabilityRequirement` | Staffing/dispatch role matrix. | `AgentCapabilityRequirementEvaluator`. | Declared before dispatch for each role/tool/skill need. | Missing skill test in `bundle://proof/SB10/transcripts/failing-first.txt`. |
| `AgentCapabilityDiagnostic` | `AgentCapabilityRequirementEvaluator`. | Runtime caller, operator diagnostics, tests. | Emitted when a role capability is missing, stale, retired, or uncataloged. | Missing and retired tests in `bundle://proof/SB10/transcripts/failing-first.txt`. |
| `IsRetiredCapability` decision | Core evaluator. | Runtime capability composition. | Applied during requirement evaluation and attached-capability resolution. | Retired skill tests in `bundle://proof/SB10/transcripts/failing-first.txt` and passing runtime filter tests. |
| Process skill/tool matrix | Core README and `candoitall-api-processes` skill. | Human and agent process operators. | Repo skill synced to active Codex skill root. | `bundle://proof/SB10/transcripts/skill-sync.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB10/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB10/transcripts/passing.txt.
- Source assertions: bundle://proof/SB10/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB10/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB10/transcripts/changed-file-hashes.txt.
