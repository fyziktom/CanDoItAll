# 00 Reentry And Harness Gate

## Status

- Status: `Ready`

## Objective

Establish the safe execution baseline before any implementation: read the original contract, inspect worktree state, identify exact test commands, confirm cognitive-memory API/runtime readiness, and initialize evidence tracking.

## Covered Inputs

- Original user request and this follow-up bundle.
- Original cognitive memory v2 bundle.
- Current repository state.
- Cognitive-memory API skill workflow.

## Prerequisites

- The repository must be available at `C:\repositories\CanDoItAll`.
- The LB4U folder must remain read-only.
- No implementation edits should start until baseline commands and current worktree state are recorded.

## Exact Source References

- C:\repositories\CanDoItAll
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-followup-lb4u-validation-refactor
- C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md

## Deliverables

- Baseline worktree status.
- Baseline build/test command list.
- Cognitive-memory API/status/database profile check plan.
- Initial workbook evidence rows.
- Updated `reviews/01-execution-report.md` gate row.

## Dependency Impact

- This subbundle unblocks every other subbundle.
- It should not change production code.
- It may add only evidence notes if needed.

## Validation Depth

- Read-only repository inspection.
- Baseline command discovery.
- API status smoke planning or execution if the app is already running.
- No behavioral conclusions unless backed by command output.

## Implementation Steps

1. Read this README, root README, original request, and execution report.
2. Run `git status --short`.
3. Identify solution/build/test commands from repo files.
4. Identify cognitive-memory test projects and targeted filters.
5. Use `candoitall-api-cognitive-memory` to check expected API status workflow.
6. Record baseline in workbook and execution report.

## Do Not Do

- Do not modify LB4U sources.
- Do not start refactoring.
- Do not ingest `routery hesla`.
- Do not claim OpenAI or Ollama validation from static inspection.

## Acceptance Checklist

- Worktree state recorded.
- Build/test command plan recorded.
- API readiness plan recorded.
- Execution report row updated.
- Next subbundle dependencies clear.

## Proof Required

- Command output summary for `git status --short`.
- Build/test command list with project paths.
- API endpoint/status plan.
- Workbook evidence row.

## Browser Validation Logging

- Browser validation is not required unless the cognitive-memory UI is opened for baseline proof.
- If opened, record route, viewport, screenshot path, and result.

## Progression Gate

- Proceed to subbundles 01 and 02 only after baseline state and test plan are recorded.

## Suggested Agent Prompt

Use this subbundle to establish the execution baseline. Do not edit production code. Record worktree state, test commands, API readiness, and evidence locations before moving on.
