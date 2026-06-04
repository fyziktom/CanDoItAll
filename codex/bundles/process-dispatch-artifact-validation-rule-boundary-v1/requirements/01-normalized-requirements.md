# Normalized Requirements

| ID | Requirement | Owner subbundles | Proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve prior MAF/provider/execution/artifact write boundaries. | SB01, SB04, SB08, SB12, SB14 | Source scans + provider/build tests |
| RQ-002 | Inventory artifact validation methods and side effects before movement. | SB02 | Method inventory + line counts |
| RQ-003 | Design validation snapshots without creating Process Core. | SB03 | Snapshot design + architecture tests |
| RQ-004 | Add guardrails before production movement. | SB04 | Failing-first tests + source scans |
| RQ-005 | Decouple expectation matching from dispatcher nested types where safe. | SB05 | Matcher tests |
| RQ-006 | Extract managed path/path normalization rules. | SB06 | Path tests |
| RQ-007 | Extract title/slug/text-content matching rules. | SB07 | Matching tests |
| RQ-008 | Prove matcher parity and review line counts. | SB08 | Regression slice + line count report |
| RQ-009 | Extract provider-native visual evidence rules. | SB09 | Visual proof matching tests |
| RQ-010 | Extract placeholder and quality validation rules. | SB10 | Negative/positive validation tests |
| RQ-011 | Extract project-structure preservation rules. | SB11 | Downgrade/defer/drop tests |
| RQ-012 | Add driver-readiness classification without driver implementation. | SB12 | Inventory + no driver-pack scan |
| RQ-013 | Run runtime smoke and no viewport proof scans. | SB13 | Build/tests/source scans |
| RQ-014 | Final red-team and next cutline. | SB14 | Final review + completed validator |
