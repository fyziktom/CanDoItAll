# SB12 Semantic Invariants

## Invariants

- Invariant ID: SB12-INV-001
- Source raw note: RN12 - Refresh documentation and Codex skills for current process runtime behavior.
- Expected behavior: Processes module docs identify the current readback and troubleshooting paths for artifact status, final delivery grounding, manager resolution, project-structure projection, and live-run policy.
- Disallowed shallow implementation: Leaving the docs as a module stub, documenting service names without operator actions, treating seeded evidence as live evidence, or introducing project/run/Tetris/Blazor-only production guidance.
- Failing-first test: bundle://proof/SB12/transcripts/failing-first.txt rejects stale process-control and seeded-evidence claims.
- Passing test: bundle://proof/SB12/transcripts/passing.txt proves diff hygiene, and bundle://proof/SB12/transcripts/source-assertions.txt proves the named runtime services appear in the docs.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/README.md; repo://docs/api-control-plane.md.
- Production assertions: Operators are directed to current-run health, invariant diagnostics, artifact status projection, `ProcessExternalTargetGroundingService`, `ProcessManagerAgentResolver`, and `ProjectStructureProcessRunFolderProjectionPolicy` instead of direct data edits.
- Red-team negative case: A future reviewer can reject docs that reintroduce active Processes MCP guidance, stale external-target proof, or seeded baseline state as live proof.
- Downstream dependency check: SB13, SB17, and SB18 can cite current operator guidance before dashboard, docs parity, and final release-readiness checks.

- Invariant ID: SB12-INV-002
- Source raw note: RN12 - Refresh template documentation and process API skill.
- Expected behavior: Template and process API docs distinguish baseline scenarios from live-run profiles, preserve `freshRunPolicy`, and describe current-run troubleshooting before mutation.
- Disallowed shallow implementation: Updating only the repository copy while the active skill remains stale, documenting live-run policy without the tool/API route, or implying baseline scenarios are live delivery evidence.
- Failing-first test: bundle://proof/SB12/transcripts/failing-first.txt rejects seeded artifacts/transitions as live delivery evidence.
- Passing test: bundle://proof/SB12/transcripts/passing.txt proves active skill hash sync; bundle://proof/SB12/transcripts/source-assertions.txt proves the skill and template README name `freshRunPolicy`, live-run profiles, and current-run evidence fields.
- Changed source files: repo://Templates/Processes/README.md; repo://codex/skills/candoitall-api-processes/SKILL.md.
- Production assertions: The active `candoitall-api-processes` skill tells users to inspect run health, artifact lineage, final delivery grounding, selected-run manager context, and live-run `freshRunPolicy` before mutating state.
- Red-team negative case: A process operator cannot claim docs parity if the active skill omits the current-run troubleshooting workflow or live-run profiles read tool.
- Downstream dependency check: SB14 generic scenarios and SB17 docs/template parity can rely on source-aligned template and skill guidance.

- Invariant ID: SB12-INV-003
- Source raw note: RN12 - Refresh MAF/Agent Framework process automation notes.
- Expected behavior: AgentFramework docs identify MAF 1.6 proof slices, adopted and guarded surfaces, process tool read/mutation boundaries, and the live-run profiles read tool.
- Disallowed shallow implementation: Stale MAF 1.0 references, prose-only proof claims, documenting unclassified tools, or allowing process agents to infer process state from prompts or database rows.
- Failing-first test: bundle://proof/SB12/transcripts/failing-first.txt rejects stale MAF 1.0 docs.
- Passing test: bundle://proof/SB12/transcripts/source-assertions.txt proves MAF process automation notes and core capability matrix include the current process tool surface.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Maf/README.md; repo://src/CanDoItAll.AgentFramework.Core/README.md.
- Production assertions: MAF docs say process agents use process API/tool surfaces, keep read tools approval-free, wrap mutation tools with approvals unless explicitly suppressed, and let Processes validate final delivery artifacts and transitions.
- Red-team negative case: Future process automation docs cannot claim readiness while bypassing process tool policy, A2A/MCP guards, or MAF 1.6 proof slices.
- Downstream dependency check: SB18 final red-team can compare docs to MAF process tool and policy proof from SB08-SB11.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Processes troubleshooting docs | Processes README and API control-plane docs. | Operators, maintainers, SB13/SB18 checks. | Refreshed after runtime/API hardening. | Stale process-control rejection in `bundle://proof/SB12/transcripts/failing-first.txt`. |
| Template/API skill guidance | Template README and synced `candoitall-api-processes` skill. | Template authors and process API users. | Repo skill copied to active Codex skill root. | Hash sync in `bundle://proof/SB12/transcripts/passing.txt` and `bundle://proof/SB12/transcripts/skill-sync.txt`. |
| MAF process automation notes | MAF README and AgentFramework Core README. | AgentFramework maintainers and automation agents. | Documents current MAF 1.6 and process tool policy boundaries. | Stale MAF 1.0 rejection in `bundle://proof/SB12/transcripts/failing-first.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB12/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB12/transcripts/passing.txt.
- Source assertions: bundle://proof/SB12/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB12/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB12/transcripts/changed-file-hashes.txt.
