# Requirement Traceability

| Requirement | Primary Subbundle | Secondary Subbundles | Proof Required |
|---|---|---|---|
| PRH-001 Runtime Package Activation Contract | SB01 | SB06 | Integration test with package assembly/executor, build, execution report |
| PRH-002 Direct Installed Manifest Discovery | SB01 | SB05 | Nested manifest test, source inspection |
| PRH-003 Generic Runtime Cleanup | SB01 | SB06 | Build without Docker default registration, source inspection |
| PRH-004 Durable Installation Logs | SB02 | SB06 | Persistence tests, plugins page browser proof |
| PRH-005 Durable Runtime Logs | SB02 | SB05 | Observer/event tests, plugin executor runtime log proof |
| PRH-006 Plugins Page Logs Subtab | SB02 | SB04 | Component test and browser screenshots |
| PRH-007 Workflow Canvas Plugin Executor Menu | SB03 | SB04 | Unit/component test and browser submenu proof |
| PRH-008 Plugin Icon Contract | SB04 | SB03, SB06 | Icon resolution tests, plugin page/menu/node screenshots |
| PRH-009 Performance And EF Hardening | SB05 | SB01, SB02 | Targeted tests/source inspection for all PERF findings |
| PRH-010 Docker Default Disable And Package ZIP Handoff | SB06 | SB01, SB04, SB05 | ZIP path/checksum, install proof, app run proof without default Docker |
| PRH-011 Validation And Proof | All | Preparation | Updated execution report and final closure gate |

## XLSX Checklist

The detailed execution checklist is stored at:

- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup\inventories\plugin-runtime-architecture-hardening-checklist.xlsx`

The workbook is the operational companion to this traceability file. If the implementation agent changes scope, it must update both the workbook and this traceability file.
