# Testability plan

## Neutral component tests

Tests should render neutral components with presentation records and callback spies only.

They must not require:

- Agent workspace services;
- provider runtimes;
- EF Core;
- PostgreSQL;
- web host startup;
- Agent execution;
- real voice;
- real attachments.

## Agent adapter tests

Adapter/facade tests prove:

- Agent records map to the same visible values and badges;
- current callbacks receive the same Agent/session identities;
- agent-only slots render in the same states;
- hidden-context parsing remains correct;
- provider/model facade behavior remains;
- settings field edits still update the existing editor model;
- floating host lifecycle commands remain with the coordinator.

## Consumer proof

At least one focused proof must exercise each existing consumer family:

- Agents page;
- floating Agent chats;
- contextual Agent windows;
- Process workspace integration.

## Negative architecture tests/guards

- neutral project has no forbidden project reference;
- neutral source has no forbidden namespace;
- no production Simple Chat UI/API reference is added;
- no new partial file expands the named large types;
- dependency graph has no cycle;
- current Agent facades are not implemented only as empty passthroughs with duplicated old markup elsewhere.
