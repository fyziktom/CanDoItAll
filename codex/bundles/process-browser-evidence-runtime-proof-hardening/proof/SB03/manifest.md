# SB03 Proof Manifest

## Status

- `Completed`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Process/template/agent instruction transcript | `bundle://proof/SB03/evidence/process-definition-agent-instruction-contracts.txt` | Passed, 9 targeted tests |
| Changed-file hashes | `bundle://proof/SB03/evidence/changed-file-hashes.txt` | Captured |
| Source assertions | `bundle://proof/SB03/evidence/source-assertions.txt` | Captured |
| Anti-stub and anti-hardcoding audit | `bundle://proof/SB03/evidence/anti-hardcoding-audit.txt` | Passed, no Tetris-specific terms in process runtime/templates/agent seeds |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof | Negative-test citation |
| --- | --- | --- | --- | --- |
| Current-run browser evidence contract in software-delivery templates | `repo://Templates/Processes/processes/software-delivery/definition.json` and sidecars | Process projection, QA step contracts, process import | `ProcessesServiceIntegrationTests.Software_delivery_template_requires_process_visible_current_run_browser_evidence_only_when_browser_workflow_is_in_scope` | Console/API process wording remains non-browser-gated |
| Agent browser proof instruction seed | `repo://Templates/Agents` and `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills` | Default delivery agents and inline skills | `AgentFrameworkWorkspaceSeedIntegrationTests` seed refresh tests | Stale managed catalog refreshes to seed version `2026-05-agent-template-teams-v12` |
| Generic runtime prompt/recovery contract | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` and recovery directive | Process-run agent prompt and retry prompt | Source assertions and dispatch tests | No Tetris-specific process-core checks |

## Completion Rule

SB03 is complete because process definitions, agent templates, inline skill seeds, prompt generation, and recovery text now require process-visible current-run browser artifacts when a visible browser workflow is in scope while preserving non-UI/API paths.
