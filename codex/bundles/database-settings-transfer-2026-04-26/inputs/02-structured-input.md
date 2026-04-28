# Structured Input

## Objectives

- Add a database-management modal that can copy selected settings from one saved database profile into another.
- Use the modal to fix ProjectStructure MCP token loss after runtime database switching.
- Prompt for basic settings transfer when a new database is created from the UI.
- Implement the transfer as a generic service with pluggable item handlers.

## Hard Constraints

- Preserve existing ProjectStructure MCP behavior and authorization semantics.
- Do not display secret/token cleartext in transfer selection UI.
- Do not copy runtime process executions as "processes"; basic settings should copy definitions/configuration only.
- Respect current database profile isolation and runtime switching rules.
- Keep module boundaries clean. Workspace must not gain references to AgentFramework or Processes.

## Initial Transfer Items

- ProjectStructure MCP token/settings.
- AI providers.
- AI agents.
- Processes.

## Validation Expectations

- Build and targeted tests for transfer services/handlers.
- Browser proof for the database-management modal, including open modal state and checkbox selection.
- Closure audit against every sentence in the raw request.
