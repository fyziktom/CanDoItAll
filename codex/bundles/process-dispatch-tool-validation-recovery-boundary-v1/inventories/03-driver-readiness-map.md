# Driver Readiness Map

This is documentation-only. Do not implement production driver APIs.

| Future driver capability | Existing validation semantics to preserve | Current bundle contribution |
| --- | --- | --- |
| Generic manager verification | read-only evidence completeness, missing-tool summaries, and blocker categorization | `ProcessRequiredToolValidationRules` and `ProcessCompletionBlockerRules` expose the semantic categories without moving orchestration or persistence. |
| SW development generic | build/test/run tool satisfaction, validation proof, and retry facts | `ProcessRequiredToolValidationRules`, `ProcessCriticalToolFailureRules`, and `ProcessRecoveryRetryDecisionRules` isolate the local rule families. |
| DotNet SW development | `workspace_dotnet_*` scaffold/build/test/run equivalence | Existing scaffold equivalence remains module-local and is validated by required-tool parity tests. |
| Rust SW development | future `cargo_*` equivalent evidence | Documentation-only semantic family; no Rust production implementation was added. |
| Browser/Web helper | browser proof screenshots/console/network/snapshot and current-attempt-only proof rules | Browser metadata requirements and current-attempt-only browser tools are preserved through `ProcessRequiredToolValidationPolicy`; browser runtime proof was not run because no UI changed. |
| Office/Excel helper | document/spreadsheet validation evidence | Documentation-only category; no document or spreadsheet driver code was added. |
| Business analysis helper | deliverable artifact satisfaction and evidence confidence | Blocker summary categories stay explicit for later mapping. |
| Manager read-only verification | ephemeral validation without state mutation | Helper rules stay pure; dispatcher still owns state mutation and final transitions. |
