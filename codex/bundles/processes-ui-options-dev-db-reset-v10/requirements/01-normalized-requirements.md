# Normalized Requirements

## Requirements

| ID | Requirement | Acceptance Signal | Owning Subbundle |
| --- | --- | --- | --- |
| R001 | Role editor options must preserve the current process template executor vocabulary instead of coercing mixed human/agent roles to a narrower executor kind. | Component test proves `person`, `agent`, `person-or-agent`, `AI agent`, and workflow selection round-trip through `ProcessRoleEditorForm`. | SB01 |
| R002 | Process step role assignment definitions must include the missing responsibility option used by templates. | `ProcessResponsibilityKind.Accountable` exists, renders in the role assignment editor, persists as a string, and template projection maps `Accountable` without fallback. | SB01 |
| R003 | Artifact expectation definitions used inside step definitions must include missing template vocabulary for decision records and approval-required trust. | `ProcessArtifactKind.DecisionRecord` and `ProcessArtifactTrustRequirement.ApprovalRequired` exist, render in artifact expectation editor, persist as strings, and projection maps them without fallback. | SB01 |
| R004 | Template vocabulary drift must be guarded by automated tests. | Test scans process template JSON values for selected contract fields and fails when a non-empty value cannot map to a supported typed option or approved alias. | SB01 |
| R005 | Development database cleanup must delete only process-owned definitions, runs, runtime history, process messages, process escalations, process outbox, process artifacts, and related process rows. | SQL transcript shows only `Processes_%` tables are truncated with cascade and before/after counts confirm non-process tables still exist. | SB02 |
| R006 | Updated process templates must be reloaded after process data is cleared. | Reload transcript shows catalog warmup/import created and published current template definitions; post-reload counts show process definitions are present and process runtime history remains clean unless an explicit baseline seed is invoked. | SB02 |
| R007 | Agents, plugins, memory, projects, project structure, and related files must remain intact. | Preservation transcript captures before/after counts for representative non-process tables and confirms no workspace file deletion command ran. | SB02 |

## Hard Constraints

- Do not drop `candoitall_development`.
- Do not truncate non-process tables.
- Do not delete `%LOCALAPPDATA%\CanDoItAll\workspace` or project structure files.
- Do not introduce new magic strings for executor kinds where a typed catalog helper can own the values.
- Do not generate XML documentation comments.
