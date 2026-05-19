# Implementation Prompt

Implement the workflow template examples bundle phase by phase.

Rules:

- Keep workflow definitions in YAML template files under `C:\repositories\CanDoItAll\Templates\Workflows`.
- Update the manifest to load new files; do not construct workflow graphs in C#.
- Preserve existing workflow keys.
- For email task workflows, avoid sending empty task arrays to `CreateTaskNodes`; route no-task outcomes to project-structure summary assets.
- For file-analysis workflows, configure source ingestion with relevant text and code extensions.
- Add targeted tests proving pack load and graph construction for the new keys.
- Update `reviews/01-execution-report.md` after each subbundle.
