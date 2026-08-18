# Project reference plan

## Expected additions

Preferred:

- add `src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj`;
- add a reference from `CanDoItAll.AgentFramework.Components`;
- add a reference from `CanDoItAll.Tests.Components`;
- add a direct reference from `CanDoItAll.Modules.AgentFramework` only when required by types it consumes directly;
- update the relevant solution/project inventory files according to repository conventions.

## Expected non-additions

Do not add a reference:

- from the neutral project to any AgentFramework project;
- from the neutral project to `Modules.LlmChats`;
- from `Modules.LlmChats` to the neutral project in Phase 1;
- from Infrastructure/Persistence to UI;
- from Process domain/runtime to UI;
- between product modules solely to access Razor components.

## Gate

Every project-reference change requires before/after CodeAnalytics dependency evidence and the architecture review gate.
