# Normalized Requirements

| ID | Requirement | Priority | Owning subbundle |
| --- | --- | --- | --- |
| RQ-001 | Remove direct `CanDoItAll.AgentFramework.Maf` project reference to `CanDoItAll.Modules.Processes`. | P0 | SB05 |
| RQ-002 | Remove all `using CanDoItAll.Modules.Processes` from MAF source. | P0 | SB05 |
| RQ-003 | Introduce a runtime tool-provider seam that lets product modules contribute `AITool` instances without MAF referencing those modules. | P0 | SB02, SB03 |
| RQ-004 | Move the current process tool builder implementation into the Processes module. | P0 | SB04 |
| RQ-005 | Preserve every existing process tool name exactly. | P0 | SB04, SB06 |
| RQ-006 | Preserve process access checks: read/write flags, allowed definition scope, project scope, grant/revoke definition behavior. | P0 | SB04, SB06 |
| RQ-007 | Preserve approval behavior: process mutation tools require approval by default; read tools remain approval-free. | P0 | SB03, SB04, SB06 |
| RQ-008 | MAF runtime must work when no Processes module is registered. | P0 | SB03, SB05, SB06 |
| RQ-009 | MAF runtime must attach process tools when Processes module is registered. | P0 | SB04, SB06, SB07 |
| RQ-010 | Add architecture guardrails preventing reintroduction of MAF -> Processes dependency. | P0 | SB01, SB05, SB06 |
| RQ-011 | Keep dispatcher behavior unchanged in this bundle. | P0 | All |
| RQ-012 | Update docs and runtime proof slices to describe provider-based tool composition. | P1 | SB08 |
| RQ-013 | Provide durable XLSX checklists so long Codex execution can resume without losing state. | P1 | Bundle preparation |
| RQ-014 | Capture artifact-backed proof manifests for critical subbundles. | P0 | SB01-SB07, SB09 |
| RQ-015 | Add final red-team audit for fake-proof resistance and hidden dependency paths. | P0 | SB09 |
