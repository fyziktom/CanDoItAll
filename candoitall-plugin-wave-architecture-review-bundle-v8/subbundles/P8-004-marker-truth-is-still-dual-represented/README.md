# P8-004 — Marker truth is still dual represented

**Severity:** Medium  
**Hard gate:** No  
**Repeat offender:** Yes

## Problem
You explicitly treat XY and markers as canonical semantics of the mindmap, not just rendering hints. Right now markers still exist both as legacy scalar fields and as MarkerSet metadata. That means future analytics, cross-project similarity, and automated improvement logic can read different truths.

## Scope
Unify marker truth so spatial/semantic analysis uses one canonical source.

## Required direction
Keep markers canonical, but pick one durable representation. Recommended: keep a structured marker set on the node core and derive any primary-marker view fields from it. Migrate legacy scalar marker values into the canonical set and remove the fallback path.
