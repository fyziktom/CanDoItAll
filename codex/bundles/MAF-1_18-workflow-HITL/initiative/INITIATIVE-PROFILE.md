# Initiative Profile

## Name

CanDoItAll MAF 1.18 Upgrade and Workflow Human-in-the-Loop Completion

## Classification

- Multi-project C# architecture change
- Framework package migration
- Production workflow orchestration boundary
- API authorization and idempotency boundary
- Persistence schema evolution
- No requested UI redesign

## Risk

**Overall risk: High**

The package update alone is moderate risk. The combined initiative is high risk because approval, checkpoint, API, and side-effect replay behavior can create silent duplicate mutations or unauthorized continuation.

## Proof profile

- SB00: Standard
- SB01: Standard
- SB02: Behavioral
- SB03: Governed
- SB04: Governed
- SB05: Governed
- SB06: Governed

## Repositories

Primary implementation repository:

- `https://github.com/fyziktom/CanDoItAll`
- branch: `development`

Bundle contract source:

- `https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills/bundles`

Upstream framework source:

- `https://github.com/microsoft/agent-framework`
- tags: `dotnet-1.17.0`, `dotnet-1.18.0`

## Baseline evidence date

2026-08-20

## Key affected layers

1. central MAF package version management;
2. MAF agent option construction and custom chat-client composition;
3. agent approval/session continuation;
4. workflow compiler and MAF adapter;
5. workflow runtime manager and external response contract;
6. checkpoint payload storage and EF persistence;
7. workflow API and authorization;
8. workflow lifecycle and integration tests;
9. API/runtime documentation.

## Explicit non-goals

- enabling parallel tool execution;
- a general user-configurable tool scheduler;
- adopting every new MAF 1.18 experimental feature;
- upgrading unrelated dependencies;
- redesigning workflow UI;
- replacing the durable workflow backend;
- claiming full distributed workflow durability for the in-process backend;
- adding human-in-the-loop to Processes outside the existing workflow integration;
- broad cleanup of unrelated MAF wrapper code.
