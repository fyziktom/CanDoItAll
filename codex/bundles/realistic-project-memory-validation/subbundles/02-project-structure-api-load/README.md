# Project Structure API Load

## Status

- Status: `Ready`

## Objective

- Create the two CanDoItAll projects and load deep, time-sliced node hierarchies through the project-structure API.

## Success Criteria

- API evidence shows project creation for both projects.
- Each stage creates a stage node, category nodes, fact/detail nodes, and a stage source file node.
- Nodes preserve parent/child structure from source-truth headings.
- `DerivedFrom` links connect structured nodes to the stage source file.
- `DependsOn` links connect sequential stages.
- Readback evidence confirms node and link counts.

## Covered Inputs

- `source-truth/source-manifest.json`
- `source-truth/ai-tap-time-sliced.md`
- `source-truth/curacao-glass-time-sliced.md`
- User requirement for deep project structures rather than file dumps.

## Prerequisites

- Subbundle 01 progression gate is passed.
- Local CanDoItAll API is reachable.
- Project-structure lease endpoints are available.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\source-manifest.json
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\ai-tap-time-sliced.md
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\curacao-glass-time-sliced.md
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\validation\load-realistic-project-memory-validation.ps1

## Deliverables

- API-created AI Tap project.
- API-created Curacao glass recycling project.
- Stage-by-stage project nodes and links.
- Stage source chunk files under `validation/evidence/<runId>/source-inputs`.
- Readback evidence files.

## Dependency Impact

- Cognitive Memory ingestion depends on project IDs, node IDs, media file nodes, and links created here.
- If hierarchy depth or metadata is wrong, recall validation will not test realistic project context.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Run the loader script against the local API.
2. Confirm PostgreSQL-backed memory status unless explicitly overridden.
3. Create projects and acquire leases.
4. Parse source-truth headings into nested nodes stage by stage.
5. Create links and read back structure after each stage.
6. Save API evidence and run summary.

## Scope Exceptions

- Direct database insertion is out of scope.

## Do Not Do

- Do not bypass leases.
- Do not write project data directly to storage.
- Do not load each original raw file as a project node.

## Acceptance Checklist

- `99-run-summary.json` contains both project IDs.
- Each project has five stages loaded.
- Readback node counts increase after each stage.
- Evidence includes node and link creation responses.

## Proof Required

- `validation/evidence/<runId>/99-run-summary.json`.
- Per-stage `*-structure-readback.json` evidence.
- Per-project created node and link counts in the run summary.

## Browser Validation Logging

- N/A. API evidence replaces browser validation.

## Progression Gate

- Memory ingestion may be trusted only after readback evidence proves both projects have nested stage, category, detail, and file nodes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use the API loader to create the projects and nested nodes from source truth, capture readback evidence, and stop if any API call fails or hierarchy depth is not preserved.
```
