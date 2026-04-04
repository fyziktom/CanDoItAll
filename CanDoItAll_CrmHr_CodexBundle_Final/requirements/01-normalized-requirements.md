# Normalized Requirements

## Core Functional Requirements

- Introduce a new CRM/HR module with a unified Party-centered domain that can represent people, organizations, delivery units, candidates, and AI agents.
- Keep current project-local participant and Workbench flows alive while enabling stable central-party linkage for reusable assignments.
- Provide CRM, HR, recruiting, AI-agent, and assignment routes under `/crm-hr` using BaseLib-first UI only.
- Integrate CRM/HR artifacts with search, activity, project/workbench assignment flows, privacy controls, and regression validation.

## Repo-Reality Requirements

- Preserve current `ProjectParticipantMetadata`, `ProjectMeetingMetadata`, and `ProjectWorkItemMetadata` semantics while extending them safely.
- Use current storage-placement and storage-reference services for any new upload, asset, or document workflows.
- Respect current project-structure dependency and checklist APIs when CRM/HR influences project or node assignment behavior.
- Reuse current Workspace AI provider-profile surfaces instead of duplicating runtime configuration in CRM/HR.

## Validation Requirements

- Critical foundations require build plus integration proof before dependent work can proceed.
- UI subbundles require real browser navigation, screenshots, and screenshot review notes.
- Final closure requires tests, browser analytics, subbundle gate results, and raw-note closure rows to be populated without pending states.
