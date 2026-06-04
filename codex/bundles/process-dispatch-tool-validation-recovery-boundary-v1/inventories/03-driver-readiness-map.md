# Driver Readiness Map

This is documentation-only. Do not implement production driver APIs.

| Future driver capability | Existing validation semantics to preserve | Current bundle contribution |
| --- | --- | --- |
| Generic manager verification | read-only evidence completeness and missing-tool summaries | tool-validation facts and blocker summary categories |
| SW development generic | build/test/run tool satisfaction and validation proof | required-tool families and implementation validation categories |
| DotNet SW development | `workspace_dotnet_*` scaffold/build/test/run equivalence | dotnet-specific rule inventory, still module-local |
| Rust SW development | future `cargo_*` equivalent evidence | semantic family map only; no Rust driver code |
| Browser/Web helper | browser proof screenshots/console/network/snapshot | metadata-required browser proof and current-attempt-only categories |
| Office/Excel helper | document/spreadsheet validation evidence | document/spreadsheet semantic categories only |
| Business analysis helper | deliverable artifact satisfaction and evidence confidence | blocker summary and artifact evidence categories |
| Manager read-only verification | ephemeral validation without state mutation | read-only/ephemeral semantics map only |
