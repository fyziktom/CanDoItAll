# Normalized Requirements

| ID | Requirement | Owning subbundle(s) | Critical |
| --- | --- | --- | --- |
| R-001 | Build canonical inventories for process operations, operation target scopes, artifact statuses, tool ids, executor ids, capability ids, enum API shape, browser proof requirements, runtime command semantics, and external-target alias boundaries. | SB01 | Yes |
| R-002 | Move internal magic strings and JSON paths behind canonical descriptors, typed wrappers, or constants where they are not external protocol boundaries. | SB01, SB02, SB04, SB05, SB07 | Yes |
| R-003 | Refactor large process dispatch responsibility centers into testable services without changing successful existing behavior. | SB02 | Yes |
| R-004 | Bind all process artifacts and runtime proof to current process run id, process step id, execution run id, project id, host profile, database profile, and relevant artifact path. | SB02, SB04, SB08, SB09 | Yes |
| R-005 | Replace or augment metric-only process costing with durable provider usage observations covering all provider-call phases. | SB03 | Yes |
| R-006 | Show known vs estimated vs unknown usage/cost in API/UI; do not answer token usage from estimated cost fields. | SB03, SB07 | Yes |
| R-007 | Ensure required finalizer, failure, repair, background, and continuation paths preserve or explicitly record provider usage status. | SB03 | Yes |
| R-008 | Harden tool policy so process step allowed operations deterministically map to available tools, browser tools, and denied-tool diagnostics. | SB04 | Yes |
| R-009 | Harden runtime command host lifecycle with keepAlive semantics, cleanup receipts, process ownership, and build-lock prevention. | SB04 | Yes |
| R-010 | Harden browser proof with route, viewport, screenshot, console, interaction, current-run binding, and no-stale-artifact validation. | SB04, SB08, SB09 | Yes |
| R-011 | Separate workflow executor preview/dry-run/commit behavior and require idempotency for external side effects. | SB05 | Yes |
| R-012 | Prevent unavailable workflow executors from being selected/executed without a clear diagnostic state. | SB05, SB07 | Yes |
| R-013 | Align agent instructions, process templates, API skills, and bundle skills with canonical contracts. | SB06 | Yes |
| R-014 | When skills or validators change, prove active Codex skill-root synchronization with repo and active hashes before downstream work. | SB06 | Yes |
| R-015 | Refactor workflow/process/provider UI editors around typed models and canonical display adapters. | SB07 | No |
| R-016 | Add a real multi-domain E2E regression suite using Tetris plus four additional simple apps uploaded through project structure and executed through the generic process path. | SB08 | Yes |
| R-017 | Add final red-team QA for stale lineage, fake proof, token undercount, side effects, host drift, and Tetris-specific assumptions. | SB09 | Yes |
| R-018 | Preserve existing working Tetris path and current process template import compatibility unless an explicit migration is included. | All | Yes |
