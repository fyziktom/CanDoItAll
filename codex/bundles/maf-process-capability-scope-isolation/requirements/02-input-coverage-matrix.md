# Input Coverage Matrix

| Raw input concern | Requirement IDs | Subbundles |
| --- | --- | --- |
| Domain leak in `WorkspaceRuntimePlugin` image prompts | REQ-MAF-001, REQ-MAF-002 | SB01, SB05 |
| Images may be analyzed for non-UI reasons | REQ-MAF-001 | SB01 |
| Dev-specific tools should have their own project | REQ-MAF-002 | SB05 |
| Processes need to add specific instructions | REQ-MAF-006, REQ-MAF-009 | SB03, SB04 |
| Processes need to limit tools, skills, MCPs | REQ-MAF-003, REQ-MAF-004, REQ-MAF-010 | SB02, SB03, SB04 |
| Forced tool or instruction carrier | REQ-MAF-005, REQ-MAF-009 | SB02, SB04 |
| Management-only step suppresses development skill | REQ-MAF-004, REQ-MAF-012 | SB02, SB06 |
| Refactor in phases with MAF first | NFR-004 | All subbundles |
| Use C# architecture skills | NFR-001, NFR-002, NFR-003 | Architecture files and all subbundles |
