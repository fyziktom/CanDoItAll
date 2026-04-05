# Subbundle 02: CRM/HR Bundle Plan Backfill

## Status

- Current state: `Completed`

## Objective

- Reconstruct the delivered CRM/HR initiative into a project hierarchy and operational plan inside the isolated app using the source bundle as the planning artifact.

## Covered Inputs

- Source CRM/HR bundle B01-B13 scope
- Umbrella project plus subproject split
- AI-agent participant ownership
- CRM AI-agent directory identities, profiles, and canonical work-item bindings for the B04 AI lane

## Prerequisites

- Subbundle `01` completed
- Source bundle hierarchy and dependency map trusted
- Authorized project-structure MCP path available

## Exact Source References

- C:/repositories/CanDoItAll/CanDoItAll_CrmHr_CodexBundle_Final/README.md
- C:/repositories/CanDoItAll/CanDoItAll_CrmHr_CodexBundle_Final/plan/01-phase-plan.md
- C:/repositories/CanDoItAll/CanDoItAll_CrmHr_CodexBundle_Final/04_PLAN/IMPLEMENTATION_SEQUENCE.md
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs

## Deliverables

- Umbrella CRM/HR project in the isolated app
- Split subprojects for the delivered workstreams
- Overview roadmap structure plus detailed subproject structures
- AI-agent participant nodes and task assignments
- Matching CRM AI-agent parties and profiles for the B04 planning lane

## Dependency Impact

- A weak reconstruction makes the final canvas review unreliable
- Poor project splitting will directly reduce management usefulness

## Validation Depth

- Confirm all major bundle areas are represented
- Confirm subproject hierarchy exists
- Confirm assigned AI-agent ownership is visible on targeted work items
- Confirm the CRM module exposes the same B04 AI agents as first-class directory records
- Confirm dependency or sequencing information is preserved where it matters

## Implementation Steps

- Create the umbrella project
- Create and attach the execution-focused subprojects
- Import or create overview roadmap nodes for the umbrella surface
- Import or create detailed work structures inside selected subprojects
- Add AI-agent participant nodes and assign relevant work items
- Create matching CRM AI-agent parties and profiles, then bind the lane and work items through canonical project-party assignments
- Query structure and analytics to confirm creation success

## Do Not Do

- Do not rerun the original implementation bundle
- Do not dump every node into one unreadable canvas
- Do not leave AI ownership only in prose notes or local-only participant nodes

## Acceptance Checklist

- Umbrella project exists
- Subprojects exist and are connected
- B01-B13 scope is represented strongly enough to control delivery
- AI-agent participants and assignments are present
- B04 AI agents are visible in CRM / HR Agents with profile metadata and canonical task bindings
- At least one dependency-oriented surface exists

## Proof Required

- API responses for project and node creation
- Structure readback showing created nodes and subprojects
- Analytics or checklist evidence for the resulting project data

## Browser Validation Logging

- Record the main project route and any detailed subproject route later reviewed in Playwright

## Progression Gate

- Proceed only if the reconstructed plan covers the whole initiative strongly enough to make the visual review meaningful

## Suggested Agent Prompt

- Backfill the CRM/HR bundle into a management-grade CanDoItAll project hierarchy with explicit subprojects, AI-agent participants, and execution-ready structure.
