# PRM-F04 — Step contracts, inputs, outputs, and evidence

## Objective

Define typed step contracts for what a step consumes, produces, validates, and hands off so responsibilities are explicit and auditable.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 1**
- Depends on: **PRM-F02, PRM-F03**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Each step can declare entry criteria, exit criteria, expected artifacts, and evidence requirements.
- Steps can declare reusable input and output contracts with type, cardinality, and notes.
- Reviewers can see required evidence before completion is allowed.
- Contract data is queryable separately from the diagram layout.

## Non-goals

- Do not rely on free-text notes alone when a structured contract field is needed.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessContractModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessContractServices.cs (new)`
- `src/CanDoItAll.Modules.Validation/* (integration hooks)`
- `src/CanDoItAll.Modules.TestLab/* (integration hooks)`
- `tests/CanDoItAll.Tests.Integration/ProcessContractIntegrationTests.cs (new)`
