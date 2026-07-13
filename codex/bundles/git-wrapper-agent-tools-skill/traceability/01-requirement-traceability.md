# Requirement Traceability

| Requirement | Inputs | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| REQ-001 | `bundle://inputs/00-original-request.md` | SB01 | Wrapper tests; `proof/SB01/source-assertions.md`; `proof/SB01/manifest.md` | Critical foundation. |
| REQ-002 | `bundle://inputs/00-original-request.md` | SB01 | Existing tests preserved plus new spec tests in `ProcessTemplateGitFoundationTests` | No parallel wrapper. |
| REQ-003 | `bundle://inputs/00-original-request.md` | SB02 | Workspace command, access metadata, and runtime composition tests; `proof/SB02/manifest.md` | Depends on SB01. |
| REQ-004 | `bundle://inputs/00-original-request.md` | SB02 | Source assertions enumerate all shipped tool names and methods | No remote/destructive tools. |
| REQ-005 | `bundle://inputs/00-original-request.md` | SB03 | Capability template materialization and assignment validation tests | Depends on SB02 final names. |
| REQ-006 | `bundle://inputs/00-original-request.md` | SB01, SB02 | Source assertions prove constants and typed git inputs; anti-stub audit | Prevent string drift. |
| REQ-007 | `bundle://inputs/00-original-request.md` | SB01, SB02, SB03 | Negative tests for forbidden path/branch/revision; grep proof for excluded remote/destructive tool names | Security gate. |
| REQ-008 | `bundle://inputs/00-original-request.md` | SB04 | Final proof manifest, command transcripts, raw closure table, and bundle validators | Closure gate. |
