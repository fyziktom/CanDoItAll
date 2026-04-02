
# Storage Touchpoints Inventory

| ID | Module | Surface | Scope | Owning phase | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Infrastructure | Baseline storage abstraction | In scope | Phase 01 / 02 | Unit + integration tests + build |
| TP-002 | Infrastructure | Storage configuration defaults | In scope | Phase 01 | Migrations + unit tests |
| TP-003 | Infrastructure | DI registrations | In scope | Phase 02 | Build + service-resolution smoke |
| TP-004 | Web | Managed files endpoint | In scope | Phase 02 | Integration tests + browser proof |
| TP-005 | Web | Program bootstrap/dev seed endpoint | In scope | Phase 04 | Integration smoke |
| TP-006 | Workbench | Project node media save | In scope | Phase 04 | Unit + Playwright + manual MCP |
| TP-007 | Workbench | Project workbench file subtype policy | In scope | Phase 01 / 04 | Unit tests |
| TP-008 | Workbench | Project structure create request composer | In scope | Phase 04 | Playwright + manual MCP |
| TP-009 | Workbench | Project structure import service | In scope | Phase 04 | Integration + Playwright |
| TP-010 | Workbench | Project workbench export/capture workflows | In scope | Phase 04 | Playwright + manual MCP |
| TP-011 | Workbench | Selection panel previews | In scope | Phase 04 | Playwright + manual MCP |
| TP-012 | Workbench | Inline document preview | In scope | Phase 04 | Playwright + screenshot review |
| TP-013 | Workbench | Preview dialog overlay | In scope | Phase 04 | Playwright MCP screenshots |
| TP-014 | Workbench | Local file opener | In scope | Phase 02 / 04 | Unit tests + manual host proof where possible |
| TP-015 | Workbench | Runtime launcher path trust | Adjacent/in scope for safety | Phase 02 / 04 | Unit tests + safety review |
| TP-016 | Factory | Attachment preparation | In scope | Phase 04 | Unit + Playwright + manual MCP |
| TP-017 | Factory | Prompt export | In scope | Phase 04 | Unit + integration |
| TP-018 | Factory | Attachment preview nodes | In scope | Phase 04 | Playwright + manual MCP |
| TP-019 | Infrastructure | Database snapshots | In scope | Phase 02 / 04 | Integration tests |
| TP-020 | Workspace UI | Settings shell | In scope | Phase 04 | Playwright + manual MCP |
| TP-021 | Workspace UI | Database source settings patterns | In scope | Phase 04 | Visual review |
| TP-022 | Resources UI/Domain | FTP resource metadata | Adjacent/in scope | Phase 01 / 04 | Design review |
| TP-023 | Resources UI | Resources page FTP editor | Adjacent | Phase 04 | Visual consistency review |
| TP-024 | Security | Secret service | In scope | Phase 01 | Unit tests + migration |
| TP-025 | Shared Model | Project object types | In scope | Phase 01 / 04 | Design review + Playwright |
| TP-026 | Workbench | Infrastructure catalog definitions | In scope | Phase 04 | Playwright + manual MCP |
| TP-027 | Playwright | Artifact browser tests | In scope | Phase 03 | Playwright tests |
| TP-028 | Playwright | App fixture | In scope | Phase 03 | Playwright tests |
| TP-029 | Unit Tests | Current local storage unit tests | In scope | Phase 03 | dotnet test |
| TP-030 | Unit Tests | Path guard tests | In scope | Phase 03 | dotnet test |
| TP-031 | Integration Tests | Managed files storage integration tests | In scope | Phase 03 | dotnet test |
| TP-032 | Integration Tests | Profile harness integration tests | In scope | Phase 03 | dotnet test |
| TP-033 | Test Support | Fake IPFS server | In scope | Phase 03 | Integration tests + honest gap logging |
| TP-034 | Support Pattern | SFTP transport implementation | Adjacent | Phase 02 | Code review |
| TP-035 | Web UI | Tuning attachments in MainLayout | Adjacent / document only | Phase 04 | Coverage audit |
| TP-036 | Migrations | SQLite model snapshot | In scope | Phase 01 | Build + migration diff review |
| TP-037 | Migrations | PostgreSQL model snapshot | In scope | Phase 01 | Build + migration diff review |

## Inventory usage

- The workbook `04-storage-driver-touchpoints.xlsx` is the authoritative working inventory for execution.
- This markdown version is a quick-review companion for QA and manager review.
