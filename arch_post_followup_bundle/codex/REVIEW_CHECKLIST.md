# Review checklist

Use this checklist at every gate and before final closure.

## Core correctness
- Is the process graph legally acyclic?
- Are self-loops rejected?
- Does runtime code assume only what the DB truly enforces?
- Does the workspace quiesce pending persistence before state-dependent actions?
- Can stale writes still bypass concurrency checks on any editor path?

## Architecture and maintainability
- Did the change reduce concentration instead of merely moving it?
- Were helper rules centralized where there was real duplication?
- Was scope kept to the active subbundle?

## Query/read-side health
- Is the workspace refresh now pulled from an intentional consistency boundary?
- Were unnecessary full-entity tracked reads reduced where they were real?
- Did the change avoid re-concentrating reads back into `ProcessesService`?

## Thread-safety and action ordering
- Could a pending background save still race publish/delete/export?
- Was any shared mutable template-pack state introduced across scopes?

## Proof integrity
- Do the `.trx` artifacts actually show the suites claimed in the report?
- Are gate memos written from live evidence?
- Does the final execution report agree with the live repository?
