# Normalized Requirements

| ID | Requirement | Owning phases |
| --- | --- | --- |
| RQ-001 | Verify latest bundle completion and preserve its narrow Core/driver decisions. | P01 |
| RQ-002 | Remove or explicitly classify the 3 current build warnings before future “clean build” gates. | P02 |
| RQ-003 | Expand Core only with pure execution/finalizer evidence descriptors, not execution/runtime behavior. | P03-P04 |
| RQ-004 | Add typed diagnostic reason/result descriptors for execution, retry, provider, finalizer and evidence consistency. | P03-P05 |
| RQ-005 | Keep module adapters as the only bridge from runtime/source payloads into Core descriptors. | P04-P06 |
| RQ-006 | Harden public Core API governance and consumer allow-list. | P06-P07 |
| RQ-007 | Prepare a driver contract proposal with permission/audit/sandbox models, but do not implement production driver APIs. | P08-P10 |
| RQ-008 | Define domain driver lane maps for .NET/Rust, Office and business-analysis as verification/read-only first. | P09-P10 |
| RQ-009 | Preserve existing runtime behavior through build, full unit tests, focused integration tests and source scans. | All |
| RQ-010 | Keep UI/mobile/browser proof out of scope unless UI files change unexpectedly. | All |
