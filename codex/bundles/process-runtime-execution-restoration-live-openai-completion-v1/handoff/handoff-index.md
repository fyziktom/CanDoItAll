# Process Runtime Restoration Handoff Index

## Status
Release-candidate handoff.

## What Is Restored
- UI/API/project-structure process launch is source-backed and covered by large-desktop Playwright proof.
- Persisted run lifecycle, step rows, project context, dispatch outbox, finalizer transitions, artifacts, managed readback, deterministic `.NET` scenario, business-analysis scenario, scheduler-origin starts, and workflow-origin starts are covered by focused integration proof.
- Run detail and recovery readback are covered by API and large-desktop browser proof.
- Operator troubleshooting exposes typed recovery state, invariant diagnostics, outbox health, escalations, and attempt timeline.
- Process driver packages remain read-only diagnostics over supplied facts.

## Current Validation Set
- Build: `bundle://proof/SB049/transcripts/release-candidate-solution-build.txt`
- Full unit: `bundle://proof/SB049/transcripts/release-candidate-full-unit-tests.txt`
- Focused process integration: `bundle://proof/SB049/transcripts/release-candidate-focused-integration-tests.txt`
- Large-desktop Playwright: `bundle://proof/SB050/transcripts/large-desktop-playwright-matrix.txt`
- Docs/source parity: `bundle://proof/SB054/transcripts/docs-source-parity-assertions.txt`
- Final validation: `bundle://proof/SB057/manifest.md`

## Non-Goals Preserved
- No generic process-driver runtime host.
- No driver registry, selector, dependency-injection auto-registration, manager command, endpoint mapping, scheduler hook, workflow hook, storage/workspace write, external call, or process mutation through drivers.
- No small/medium/mobile UI closure beyond the scoped large-desktop Playwright proof.
- Live OpenAI proof remains opt-in and was not counted as a deterministic pass.

## Handoff Files
- Run instructions: `bundle://handoff/run-instructions.md`
- Future driver prerequisites: `bundle://handoff/execution-capable-driver-prerequisites.md`
- Execution report: `bundle://reviews/01-execution-report.md`
- Final handoff proof: `bundle://proof/SB060/manifest.md`
