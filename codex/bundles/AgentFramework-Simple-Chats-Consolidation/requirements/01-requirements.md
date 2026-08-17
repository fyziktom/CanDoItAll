# Requirements

The authoritative machine-readable list is requirements.json. Requirements are grouped here for execution review.

## Baseline and compatibility

- ASCC-001 through ASCC-006 freeze the source and preserve all existing Agent and Simple Chat behavior.
- ASCC-002 and ASCC-045 make the HTTP, authorization, persistence, migration, and transfer compatibility surfaces explicit.

## Architecture

- ASCC-007 through ASCC-016 define the target MAF projects, ownership, dependency direction, partial-class policy, and one-time composition.
- Core and Application are separate projects even though SB03 migrates them in one compile-safe work unit.
- Runtime and Persistence are separate because provider execution and EF/database-profile fencing change for different reasons.
- Components has no route, persistence, runtime, Web, or Agent-module dependency.

## Providers and costs

- ASCC-017 through ASCC-030 preserve canonical provider ownership and establish a neutral typed usage projection.
- ProviderUsageWorkloadKind has atomic producers Agent and SimpleChat.
- ProviderUsageWorkloadSelection is a validated flags value: Agents, SimpleChats, or Both; None is invalid.
- Agent evidence and Simple Chat invocation evidence stay in their operational stores.
- OperationId plus Ordinal is the Simple Chat attempt deduplication identity.
- Historical tokens without price provenance remain unpriced rather than being repriced or shown as free.

## UI

- ASCC-031 through ASCC-043 put Simple Chats next to Agents, preserve inner workspace tabs and floating behavior, consolidate navigation, and scope the existing cost dashboard.
- Both is the default usage scope.
- Catalog/configuration totals do not change with usage scope.
- Provider/model usage and cost do change with scope.
- Consumer rankings retain Agent versus Simple Chat semantics.

## Proof

- ASCC-044 through ASCC-048 define sensitive-data handling, compatibility guards, architecture proof, named browser scenarios, and final closure.

