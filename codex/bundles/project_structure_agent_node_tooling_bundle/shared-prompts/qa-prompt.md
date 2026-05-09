# QA Prompt

Review the implementation against the raw request, not only the code diff.

- Confirm page title is exactly `PS - <project name>` or truncated with `...`.
- Confirm the node catalog includes all ProjectObjectType values and the important typed subtypes from the canvas catalog.
- Confirm `WorkItem/task` is explicitly documented in tool guidance.
- Confirm contextual prompts include selected-node IDs and do not require guessing from visible UI text.
- Confirm selected-node transfer creates a child project, moves selected nodes and descendants, attaches moved roots to the target root, keeps moved descendants under moved parents, preserves internal `DependsOn` links, and removes invalid cross-project links.
- Verify the XLSX workbook includes architect examples and additional candidate one-call scenarios.
