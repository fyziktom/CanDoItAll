# Normalized Requirements

| Requirement | Details |
|---|---|
| R-001 Full-width modal | The fullscreen assignment dialog body and header copy must use the available modal width without artificial max-width causing dead space. |
| R-002 All summary mode | Add a first rail item named `All`; selecting it renders the existing all-role summary overview and bottom selected-agent detail. |
| R-003 Role assignment mode | Selecting a role renders only that role's candidate assignment view, headed by the role metadata. |
| R-004 Candidate ordering | In role assignment mode, show selected/main candidate first, then other resolvable candidates ordered by numeric score descending and name as a tie-breaker. |
| R-005 Directory plus card | The last card in role assignment mode is a large plus/icon card that opens the existing all-agents picker. |
| R-006 Candidate badges | Agent cards in summary and role views show compact `model`, `tools`, `skills`, and `details` badges. |
| R-007 Tooltip behavior | `model`, `tools`, and `skills` badges use the existing tooltip service and show provider/model or capability names. |
| R-008 Readonly details | `details` opens a readonly dialog with identity, runtime, recommendation, tools, and skills information. |
| R-009 Validation | Targeted tests, build, browser screenshots, and bundle reports must prove the shipped behavior. |
