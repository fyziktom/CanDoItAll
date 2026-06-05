# Normalized Requirements

| ID | Requirement | Priority | Verification |
| --- | --- | --- | --- |
| RQ-001 | Review previous execution/retry/provider refactor and preserve completed behavior. | P0 | Source/proof audit before production movement. |
| RQ-002 | Do not create Process Core yet. | P0 | Source scan for `CanDoItAll.Processes.Core` and forbidden references. |
| RQ-003 | Do not introduce production process-driver APIs. | P0 | Source scan for `IProcessDriverPack`, `IProcessDriverRegistry`, driver packages. |
| RQ-004 | Extract session-state observation into module-local helpers. | P0 | Focused session observation tests and source assertions. |
| RQ-005 | Extract execution-log/tool observation helpers without behavior drift. | P0 | Focused execution log/browser output tests. |
| RQ-006 | Extract declared step outcome parsing and branch selection helpers. | P0 | Focused governed outcome/branch tests. |
| RQ-007 | Extract completion status decision inputs and helper rules. | P0 | Focused completion status parity tests. |
| RQ-008 | Extract completion reason builder helpers. | P1 | Focused reason text tests and snapshot parity. |
| RQ-009 | Keep side effects in existing dispatcher/coordinator surfaces only. | P0 | Source scan for forbidden side-effect tokens in pure helpers. |
| RQ-010 | Slim `ToolValidation.cs` meaningfully, target below 1400 lines if safe. | P1 | Line-count transcript and source review. |
| RQ-011 | Keep browser/UI proof N/A and avoid small/medium/mobile artifacts. | P0 | Changed-file and proof-path scan. |
| RQ-012 | Add documentation-only driver-readiness map for observation/outcome evidence. | P1 | Markdown map and no production API scan. |
| RQ-013 | Use many staged subbundles with critical gates. | P0 | SB01-SB48 gate table and manifests. |
