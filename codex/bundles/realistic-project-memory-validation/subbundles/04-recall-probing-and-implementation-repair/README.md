# Recall Probing And Implementation Repair

## Status

- Status: `Ready`

## Objective

- Probe Cognitive Memory recall against source-truth questions, analyze missing context or incorrect summaries, and repair the C# implementation only when evidence identifies an actionable defect.

## Success Criteria

- Every manifest recall probe runs.
- Analysis reports context coverage, expected source locator coverage, and required-term coverage.
- Failures are categorized before any implementation change.
- If code changes are needed, the root cause, files changed, build/test proof, and rerun recall evidence are recorded.

## Covered Inputs

- `source-truth/source-manifest.json` recall probes.
- `validation/evidence/<runId>/99-run-summary.json`.
- Cognitive Memory recall API responses.
- Source-truth markdown used as comparison baseline.

## Prerequisites

- Subbundle 03 progression gate has passed or produced a blocking failure with evidence.
- Recall endpoint is reachable.
- The latest run summary exists under `validation/evidence`.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\validation\analyze-realistic-project-memory-quality.ps1
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\validation\load-realistic-project-memory-validation.ps1
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\source-manifest.json
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\project-structure-mindmap.mmd

## Deliverables

- Recall evidence per stage/project.
- `95-memory-quality-analysis.json`.
- `96-memory-quality-analysis.md`.
- Optional C# patch and verification evidence if repair is necessary.

## Dependency Impact

- This subbundle is the final validation gate for whether Cognitive Memory is useful as a project discussion partner.
- Any repair must preserve existing memory boundaries and policy behavior.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run recall probes from the loader or rerun against the latest run summary.
2. Run the memory-quality analyzer.
3. Inspect missing context, missing locators, and missing required terms.
4. Determine whether failure is caused by source data, review policy, ingestion/consolidation, recall retrieval, source-locator handling, or app code.
5. Patch C# only for proven app defects.
6. Build/test and rerun affected validation.
7. Update execution report with final evidence.

## Scope Exceptions

- A recall answer may paraphrase; exact prose matching is not required.
- Required-term checks are a minimum quality gate and do not replace human review of context usefulness.

## Do Not Do

- Do not patch code to satisfy malformed bundle data.
- Do not accept empty context packs as successful.
- Do not hide provider or policy failures behind fallback behavior.

## Acceptance Checklist

- All probes are accounted for.
- Analysis findings are either `Passed` or have an explicit root-cause path.
- Any code repair has build/test proof and rerun evidence.

## Proof Required

- `validation/evidence/<runId>/95-memory-quality-analysis.json`.
- `validation/evidence/<runId>/96-memory-quality-analysis.md`.
- If repaired: relevant `dotnet build`/`dotnet test` output and post-repair API evidence.

## Browser Validation Logging

- N/A. API and command evidence replace browser validation.

## Progression Gate

- The bundle can close only when recall analysis passes or all remaining failures are explicitly classified with evidence and a justified scope decision.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Run recall analysis against the source-truth manifest, inspect failures before editing app code, repair only proven Cognitive Memory defects, and capture post-repair evidence.
```
