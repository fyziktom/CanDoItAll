# Requirement Traceability

| Requirement | Normalized requirement | Owning subbundle(s) | Planned proof |
| --- | --- | --- | --- |
| RQ-001 | Remove direct MAF -> Processes project reference | SB05 | Static csproj assertion + source grep + build |
| RQ-002 | Remove Processes namespace usage from MAF | SB05 | Static source assertion |
| RQ-003 | Introduce runtime tool-provider seam | SB02, SB03 | Unit tests with fake provider and zero providers |
| RQ-004 | Move process tool builder into Processes | SB04 | Source assertions + parity tests |
| RQ-005 | Preserve all process tool names | SB04, SB06 | Exact-name parity tests against inventory |
| RQ-006 | Preserve access checks | SB04, SB06 | Access denial/grant/revoke tests |
| RQ-007 | Preserve approval behavior | SB03, SB04, SB06 | Approval wrapper tests |
| RQ-008 | MAF works without Processes | SB03, SB05, SB06 | Runtime composition test with zero process provider |
| RQ-009 | MAF attaches process tools with Processes | SB04, SB06, SB07 | DI/composition smoke |
| RQ-010 | Architecture guards prevent recurrence | SB01, SB05, SB06 | Static regression tests |
| RQ-011 | Dispatcher unchanged | All | No dispatcher move; targeted process smoke |
| RQ-012 | Docs updated | SB08 | README/architecture doc source assertions |
| RQ-013 | XLSX checklist provided | Bundle prep | Workbook exists and references subbundles |
| RQ-014 | Critical proof manifests | SB01-SB07, SB09 | `proof/SBxx/manifest.md` paths |
| RQ-015 | Final red-team audit | SB09 | Red-team closure artifact |
