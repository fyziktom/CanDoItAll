# P8-003 — Node-kind registry is not yet the authoritative capability matrix

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

## Problem
The new registry is a real improvement, but assignment rules and canonical-node scope policy are still hardcoded elsewhere. That splits node semantics between the registry, the workbench page, and CRM/HR services. When plugins or agents start assigning parties or node capabilities dynamically, these seams will drift.

## Scope
Promote the registry into a true capability and assignment-policy owner.

## Required direction
Extend the descriptor model so one registry resolves allowed party-assignment roles, required canonical-node scope, participant role interpretation, and other node-scoped capabilities. UI and CRM/HR validation must query the registry instead of shipping private switch statements.
