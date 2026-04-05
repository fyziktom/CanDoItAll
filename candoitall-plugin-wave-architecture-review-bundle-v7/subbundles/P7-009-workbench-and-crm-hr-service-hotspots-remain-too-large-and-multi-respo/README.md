# P7-009 - Workbench and CRM/HR service hotspots remain too large and multi-responsibility

- Severity: Medium
- Gate: Watch
- Status: Open
- Repeated from: PW6-011

## Problem

ProjectWorkbenchModels.cs is still over 3200 lines and CrmHrServices.cs is still over 5000 lines. Even when local changes are correct, future plugin wave changes will be expensive and regression-prone if these seams keep accumulating behavior.

## Required direction

Split orchestration by reason-to-change: assembly, node commands, lifecycle/history, relations, facets/bindings, assignments, party directory, and connector-facing orchestration.

## Closure proof

Hotspot files are decomposed or at least the new extension seams isolate future connector work away from the hotspots.
