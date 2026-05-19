# Structured Input

## Objectives

- Add team records to the AgentFramework organization catalog.
- Expose team CRUD and multi-agent membership editing inside the Agents tab.
- Reuse the shared `TreeView` component for team/agent navigation and filtering.
- Reuse `AgentSelectionCard` card selection inside a modal for multi-select membership updates.
- Add process launch HR matching UX that allows selecting an agent team before running HR matching.
- Mark selected or recommended candidates that are outside the chosen team.

## Hard Constraints

- Teams belong to the AgentFramework/Agents module, not CRM-HR.
- Membership is many-to-many: teams have many agents and agents may appear in many teams.
- Existing agent creation/editing and CRM-HR projection behavior must continue to work.
- Matching must remain role-complete: if a selected team lacks a required role fit, HR matching may choose an out-of-team candidate and must mark it.

## Validation Expectations

- Unit or component test for team persistence and deletion/member pruning.
- Component test for tree filtering and multi-select membership modal.
- Integration test for HR matching with selected team and out-of-team candidate marker.
- Browser validation for `/agents?tab=agents` and process launch planning.
