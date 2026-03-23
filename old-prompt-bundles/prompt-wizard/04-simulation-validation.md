# Simulation Validation

| Case | Flow | Result | Validation Focus | Missing Groups | Missing Stack Tags | Missing Roles |
| --- | --- | --- | --- | --- | --- | --- |
| CanDoItAll Branch-Aware Prompt Flow Visualization | architecture-review-plan-implement-validate | PASS | architecture, ui, cross-db, docker-tests | - | - | - |
| PHP Canvas Calendar Recurring Events and Drag/Drop | ui-canvas-feature-delivery | PASS | canvas-interaction, browser-proof, accessibility | - | - | - |
| Safe Refactor of Prompt Factory Context Assembly | audit-plan-refactor-review | PASS | regression-proof, docker, backward-compatibility | - | - | - |
| Offline Entitlements and Sync-Ready Account Feature | fullstack-offline-feature | PASS | sync, cross-db, migration, offline-proof | - | - | - |
| M5Stack MIDI Piezo Hit Engine Refinement | embedded-midi-firmware-tuning | PASS | timing, hardware-constraints, telemetry, manual-hardware-checks | - | - | - |

## Cases
## CanDoItAll Branch-Aware Prompt Flow Visualization
- Summary: Add branch-aware prompt-run visualization and lineage-aware validation to the existing .NET/Blazor Prompt Factory and Workbench.
- Flow: architecture-review-plan-implement-validate
- Required stack tags: .net, blazor, efcore, sqlite, postgresql, tailwind, playwright, offline-first
- Expected roles: role-architecture-lead, role-senior-reviewer, role-implementation-planner, role-implementation-lead, role-test-validation-lead
- Extra block inserts: stack-dotnet-solution, stack-blazor-webapp, stack-tailwind-css, stack-efcore, stack-sqlite, stack-postgresql, stack-playwright-mcp, stack-offline-first-sync, toolbox-run-unit-tests-docker, toolbox-run-ui-tests-docker

## PHP Canvas Calendar Recurring Events and Drag/Drop
- Summary: Extend a PHP app with canvas-first recurring events, drag/drop, and Outlook-like dense layouts validated in a real browser.
- Flow: ui-canvas-feature-delivery
- Required stack tags: php, html, javascript, css, canvas, playwright
- Expected roles: role-ui-ux-engineer, role-implementation-planner, role-implementation-lead, role-test-validation-lead
- Extra block inserts: stack-php-webapp, stack-html-js-css, stack-canvas-html-js, stack-playwright-mcp, toolbox-use-playwright-mcp-now, toolbox-capture-browser-artifacts

## Safe Refactor of Prompt Factory Context Assembly
- Summary: Refactor the CanDoItAll prompt context assembly pipeline for lower coupling while preserving behavior and locking regressions with Docker tests.
- Flow: audit-plan-refactor-review
- Required stack tags: .net, blazor, efcore, sqlite, playwright
- Expected roles: role-refactor-specialist, role-implementation-planner, role-senior-reviewer
- Extra block inserts: stack-dotnet-solution, stack-blazor-webapp, stack-efcore, stack-sqlite, stack-playwright-mcp, toolbox-run-unit-tests-docker, toolbox-run-integration-tests-docker, toolbox-cache-downloads-mobile-data

## Offline Entitlements and Sync-Ready Account Feature
- Summary: Add an offline-first entitlements and sync-ready account workflow spanning API, EF Core, PostgreSQL, SQLite, and Blazor UI.
- Flow: fullstack-offline-feature
- Required stack tags: .net, blazor, efcore, postgresql, sqlite, offline-first, playwright
- Expected roles: role-architecture-lead, role-senior-reviewer, role-implementation-planner, role-implementation-lead, role-test-validation-lead
- Extra block inserts: stack-dotnet-solution, stack-blazor-webapp, stack-efcore, stack-postgresql, stack-sqlite, stack-offline-first-sync, stack-playwright-mcp, toolbox-cross-db-compat-check, toolbox-db-migration-dry-run, toolbox-run-integration-tests-docker

## M5Stack MIDI Piezo Hit Engine Refinement
- Summary: Refine a piezo-first hit engine on M5Stack with better timing, telemetry, and host-side validation.
- Flow: embedded-midi-firmware-tuning
- Required stack tags: arduino, m5stack, midi, audio, .net, blazor
- Expected roles: role-embedded-midi-engineer, role-implementation-planner, role-test-validation-lead
- Extra block inserts: stack-arduino-firmware, stack-m5stack, stack-midi-audio, stack-dotnet-solution, stack-blazor-webapp, toolbox-generate-fixtures-and-seed-data, toolbox-create-manual-qa-checklist
