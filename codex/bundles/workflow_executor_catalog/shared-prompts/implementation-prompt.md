# Implementation Prompt

Execute the bundle at `repo://codex/bundles/workflow_executor_catalog` one subbundle at a time. Run the prepared-stage validator before production edits. Before each subbundle, confirm prerequisites, exact source references, and owned requirements. Implement the smallest correct change, capture failing-first and passing proof where behavior changes, update proof under `bundle://proof/SBxx/`, update `reviews/01-execution-report.md`, then run the subbundle closure gate before moving on.

