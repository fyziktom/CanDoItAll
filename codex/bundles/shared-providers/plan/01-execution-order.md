# Execution order

## Phase A: architecture and contracts

- SB00 characterizes current code, captures dependency graph, and locks decisions.
- SB01 creates lower-level protocol/ports and cross-cutting access context.
- SB02 creates PostgreSQL entities, migration, state machine, and application services.

## Phase B: central and client backend

- SB03 implements publication/catalog/auth/ETag.
- SB04 implements the bounded compatibility relay, images, streaming, usage/audit.
- SB05 implements source client, URI policy, sync, selection, reconciliation.
- SB06 wires the shared connector into Workspace and AgentFramework effective profiles.

## Phase C: mandatory backend proof

- SB07 creates deterministic upstream and three-app Compose topology.
- It runs positive/negative scenarios from a clean state.
- UI remains locked until the backend review gate is `PASS`.

## Phase D: UI

- SB08 implements central publication and client source/import management.
- SB09 provides component/Playwright/screenshot/accessibility proof.

## Phase E: delivery and closure

- SB10 finalizes repeatable scripts, docs, troubleshooting, and manual workflow.
- SB11 freezes API, captures OpenAPI, and updates SharedInfo/new skill.
- SB12 performs the one stable aggregate, final clean Docker lane, leaves stack running, and
  closes traceability.

## Per-subbundle loop

1. Re-read current code and relevant architecture.
2. Run bundle/subbundle readiness validators.
3. Mark one subbundle `IN_PROGRESS`.
4. Capture before reference/symbol state.
5. Implement the smallest complete behavior.
6. Build affected production projects.
7. List and run focused tests.
8. Run architecture/security checks owned by the subbundle.
9. Complete proof and handoff.
10. Mark `DONE` and unlock exactly one next subbundle.
