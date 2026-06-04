# SB10 — Documentation and architecture guard refresh

## Status

- Status: `Completed`

## Objective

Update live docs and static guardrails after providerizing project-structure/image tools and hardening process provider purpose behavior.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

- `SB09` must be complete and its progression gate must have passed.

## Exact Source References

- repo://README.md
- repo://docs/architecture-beta.md
- repo://src/CanDoItAll.AgentFramework.Maf/README.md
- repo://src/CanDoItAll.Modules.Processes/README.md
- repo://codex/skills/candoitall-api-processes/SKILL.md

## Deliverables

- Docs describe providerized first-party tools without claiming process-core extraction.
- Architecture tests document allowed MAF references and expected removal trajectory.
- Stale reference scan for old hard-coded attach method names.
- Operator troubleshooting updated for provider diagnostics.

## Dependency Impact

- Moderate dependency impact; downstream proof must still include regression checks.

## Validation Depth

- This subbundle requires source assertions, targeted tests, and proof transcripts. Compile-only proof is not sufficient when tool-provider behavior changes.

## Implementation Steps

1. Open every exact source reference and confirm current branch shape.
2. Create or update the smallest set of source files needed for this subbundle.
3. Preserve existing public tool names and policy behavior unless this subbundle explicitly owns the change.
4. Run targeted proof before broader build proof.
5. Record source assertions, test transcripts, and any reopen triggers.
6. Update the execution report and stop at the progression gate.

## Scope Exceptions

- No process-core extraction.
- No process driver packs.
- No unrelated UI work.

## Do Not Do

- Do not silently rename or drop existing tools.
- Do not weaken approval or access policy.
- Do not use broad cleanups that touch unrelated modules without explicit inventory.
- Do not mark placeholder proof as passed.

## Acceptance Checklist

- [x] Source inventory for this slice is recorded.
- [x] Implementation is limited to this subbundle scope.
- [x] Tool parity/access/approval behavior is proven where applicable.
- [x] Static dependency scans are updated where applicable.
- [x] Targeted tests pass.
- [x] Full or relevant project build pass is recorded.
- [x] Execution report is updated.

## Proof Required

- `stale reference scan`
- `static architecture tests`
- `docs link/source assertion tests`
- `git diff --check`

## Browser Validation Logging

- N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

- Docs must be live-source accurate; historical bundle mentions may remain only when classified as historical.

## Completion Notes

- Root, architecture, MAF, Processes, and process-skill docs now describe the providerized first-party tool boundary: Processes owns `ProcessAgentRuntimeToolProvider`, Workbench owns `ProjectStructureAgentRuntimeToolProvider`, and the AgentFramework module owns `ImageGenerationAgentRuntimeToolProvider`.
- Architecture docs explicitly state this is a dependency-inversion seam, not a completed process-core extraction.
- Operator troubleshooting now points to provider key/display name, expected process tool count, `AgentProcessAccessMetadata`, and optional runtime-provider receipt/trace ownership fields.
- `ApiDocsSkillsParityTests` now asserts the live docs and skill text keep this boundary and diagnostic guidance current.
- The stale-reference scan found no removed hard-coded attach method names in live source, docs, or installed skills.
- Proof is recorded in `bundle://proof/SB10/manifest.md` and `bundle://proof/SB10/semantic-invariants.md`.

## Suggested Agent Prompt

Implement SB10 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
