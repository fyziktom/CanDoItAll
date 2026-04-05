# P8-002 — Hierarchy is still dual represented and dual written

**Severity:** Critical  
**Hard gate:** Yes  
**Repeat offender:** Yes

## Problem
A node's parent relationship is still stored twice: once as ParentNodeKey and again as persisted hierarchy links. That means one structural fact has two durable owners. This is exactly the kind of drift source that will become painful under bulk refactors, plugins, agents, and cross-project movement.

## Scope
Collapse editable-node hierarchy to one owner and derive links at assembly time.

## Required direction
Choose one canonical hierarchy owner for editable nodes. The simplest path is to keep ParentNodeKey canonical and derive hierarchy edges in assembly/view models only. Generic link rows should remain only for non-hierarchy relationships. Delete editable-node Contains/BelongsTo persistence from create/reparent/move flows and add a data migration to clean historical duplicates.
