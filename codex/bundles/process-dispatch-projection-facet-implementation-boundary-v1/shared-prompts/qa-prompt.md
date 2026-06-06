# QA / Red-team Prompt

Review whether the implementation only refactors architecture and preserves runtime behavior.

Reject the implementation if:
- `CanDoItAll.Processes.Core` appears.
- production driver APIs appear.
- UI files or mobile/small/medium proof artifacts appear.
- projection source-family order changes.
- a broad host or all-facet service implementation remains.
- source coordinators take `ProcessRunAutomationDispatchService` or an all-facet host.
- candidate mutation is duplicated.
- tests are build-only without focused projection proof.
