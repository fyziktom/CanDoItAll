# P8-001 — Core node / binding boundary is still not sealed

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

## Problem
The universal node carrier is still physically mapped with binding/media/artifact columns and the public metadata contract still exposes foreign-owner IDs. That keeps ownership blurry and invites future connector or CRM data to leak back into the core node record.

## Scope
Seal the node core/binding split without demoting node to a mere view.

## Required direction
Keep node as the universal carrier for identity, mindmap structure, XY, canonical markers, schedule, text, status, and subtype. Finish the separation by moving all binding/media/artifact/foreign-reference persistence behind binding/reference/facet records. Remove the binding columns from the mapped ProjectObjectRecord schema or make them transitional non-mapped runtime accessors only. Narrow metadata so foreign-owner IDs are not first-class writable payload.
