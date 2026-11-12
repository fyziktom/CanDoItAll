# Product, Domain, and UX Reconstruction

This is the product-design reconstruction for CanDoItAll. It describes the meaning of
the product before any particular page layout, component library, visual language, or
implementation is chosen. It is intentionally grounded in source evidence rather than
invented explanatory copy.

## Product in one sentence

CanDoItAll is a local-first application for governing project delivery: users turn
intake into projects and executable plans, coordinate human and AI workforce, define
and run repeatable processes and workflows, and retain an inspectable record of
automated activity.

**Evidence:** repository [README](../../README.md),
[module map](../architecture/modules.md), and the product routes and contracts listed in
[evidence index](resources/evidence-index.md).

## What this documentation is and is not

This is the design-facing source for:

- user-facing vocabulary and the relationship between product entities;
- the jobs the product supports, its major user journeys, and safety boundaries;
- information architecture and screen-level responsibilities; and
- redesign principles that preserve product meaning while leaving visual implementation
  open.

It is not a component catalogue, a CSS specification, a replacement for API/OpenAPI
contracts, or a claim that every implementation detail is intended product behaviour.
The existing Blazor/Components implementation is evidence of a current surface, not the
future design system.

## Reading order

1. [Domain model and vocabulary](domain-model.md) defines the nouns, ownership, and
   distinctions that UI copy and navigation must preserve.
2. [Scenarios and UX principles](scenarios.md) describes the user problems and the
   expected result of important flows.
3. [Information architecture](information-architecture.md) maps those concepts to the
   current product surfaces.
4. [Screen contracts](screen-contracts.md) specify what each in-scope product surface
   lets a user see and do, independent of today’s layout. Generic runtime recovery
   routes are intentionally out of scope.
5. [Redesign brief](redesign-brief.md) translates product meaning into criteria for a
   new user experience before visual-system choices are made.
6. [Test scenario evidence](resources/test-scenario-evidence.md) records which scenario
   claims have been checked against actual test assertions and what still needs review.
7. [Evidence index](resources/evidence-index.md) records where claims came from and how
   to extend the reconstruction without guessing.
8. [Product-owner walkthrough validation](resources/product-owner-walkthrough-2026-08-23.validation.md)
   compares the recorded end-to-end demonstration with this reconstruction. The raw,
   timestamped Czech transcript is retained beside it.

## Evidence convention

Each substantive claim is classified by its strongest evidence:

- **Confirmed** — expressed by a public route/contract, persisted model, or maintained
  product documentation.
- **Corroborated** — also exercised by a component or integration test and represented
  in a page or UI string.
- **Inference** — a design interpretation drawn from confirmed evidence. It must be
  challenged or promoted to confirmed before becoming a hard product requirement.

The documents avoid treating lengthy current UI prose as source truth. Short labels,
states, commands, route names, contracts, and tests are more reliable reconstruction
evidence.

## Product-design invariants

These are the cross-cutting constraints a redesign must retain.

- **Governance and traceability over magic.** Automated work has durable operations,
  run status, artifacts, events, approvals, or recovery paths where applicable.
- **A project is the delivery context.** Its structure, plans, files, linked processes,
  workforce assignments, and schedule views must remain understandable as one context.
- **Human and AI workforce are related but not silently interchangeable.** The product
  models both as assignable/operational participants while retaining their distinct
  records, capabilities, and governance needs.
- **Definitions and executions are different things.** Reusable process, workflow, and
  chat definitions have their own lifecycle; runs and conversations preserve the
  context/revision from which they started.
- **Safety decisions are explicit.** Destructive work, scripts, external tools, tokens,
  secrets, provider access, and ambiguous execution recovery need clear ownership and
  a visible decision point.
- **Automation authority is scoped, not implied.** An agent needs explicit authority
  for consequential work such as changing a project structure, using a storage tool,
  or progressing past an approval/escalation. The available authority should be legible
  in the delivery context.
- **The active workspace/database is meaningful context.** Data, runtime capabilities,
  storage, and workbench isolation can change with the selected database profile.

The last four bullets are **Corroborated** by the referenced routes, pages, and tests;
the first is a design interpretation of those mechanisms (**Inference**).

## Open questions

- Which user roles and permission model should be expressed in the product UX beyond
  current API scope/capability enforcement?
- Is `Workflow` a user-facing peer of `Process`, or is it primarily an automation
  building block surfaced inside AgentFramework? Current routes support both readings.
- What is the intended relationship and boundary between agent chat sessions and Simple
  Chats in a future navigation model?
- Which delivery roles own Test Lab plans and evidence in practice (for example QA lead,
  delivery lead, or responsible party)? The aggregate is confirmed; its role model is
  not.

These questions are deliberately retained rather than answered from names alone.
