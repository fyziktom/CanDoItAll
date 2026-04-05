# P7-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth

- Severity: Critical
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-001

## Problem

Structure, calendar, and other flows still materialize Projects, hierarchy, Resources, Prompt Factory, Validation, and TestLab data into Workbench canonical tables through SyncGraphAsync. That means Workbench storage still mixes real project-owned nodes with mirrored projection nodes. This blocks a clean plugin platform because future connectors would inherit the same anti-pattern.

## Required direction

Remove persisted SyncGraph-as-storage behavior from Workbench canonical tables. Keep project-owned nodes canonical, but assemble cross-module read-only surfaces through contributor services or explicit read-model tables outside Workbench_ProjectObjects / Workbench_ProjectObjectLinks.

## Closure proof

No SyncGraphAsync method or call remains in Workbench read flows; no system-managed cross-module nodes are persisted into Workbench canonical tables; new assembly contributor layer and guardrail tests exist.
