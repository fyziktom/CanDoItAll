# P7-003 - Node-kind semantics and node-scoped capability rules are still fragmented and hardcoded

- Severity: Critical
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-003 + PW6-009

## Problem

Node meaning is still split across ProjectObjectType, ObjectSubtype strings, create catalog definitions, editor mapping, and CRM/HR hardcoded role checks. This prevents clean extensibility for new connector-driven block types and for richer node-scoped assignments to people, agents, and partners.

## Required direction

Introduce a central ProjectNodeKindRegistry / descriptor model with family, allowed relations, allowed party roles, editor schema, transition rules, facet owner, and command exposure. Create/edit/reclassify/UI/CRM-HR node scope validation must all consume this registry.

## Closure proof

A node-kind registry exists; page code no longer hardcodes ResolveNodeAssignmentRoles/ResolveParticipantRole; CRM-HR no longer hardcodes RequiresCanonicalNode / IsAllowedNodeType for node-scoped roles.
