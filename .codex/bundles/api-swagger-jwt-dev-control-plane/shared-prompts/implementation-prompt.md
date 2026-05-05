# Implementation Prompt

Implement the selected subbundle only. Keep HTTP handlers thin and reuse existing services:

- Projects: `ProjectsService`
- Project structure: existing `ProjectStructureAgentApi` and its services
- Processes: `ProcessesService`
- Agents: `IAgentFrameworkWorkspaceService`

Do not write direct EF code when a service method already exists. If a missing helper creates endpoint duplication, add a small shared helper at the HTTP boundary and record the architecture decision in the execution report.
