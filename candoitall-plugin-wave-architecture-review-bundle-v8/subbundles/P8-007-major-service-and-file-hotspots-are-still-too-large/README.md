# P8-007 — Major service and file hotspots are still too large

**Severity:** Medium  
**Hard gate:** No  
**Repeat offender:** Yes

## Problem
Correctness improved, but maintainability remains at risk. Several files are still large enough that architectural drift can hide inside them even when the public design is improving. This matters because the next wave will expand CRM/HR and connector surfaces further.

## Scope
Shrink the hotspots once the hard gates are closed so future drift has fewer hiding places.

## Required direction
Split hotspot files by responsibility while the phase8 refactor is in flight. This should follow the canonical changes above rather than happen as a cosmetic move first.
