# Multi-Domain Process Test Harness Architecture

## Goal

Run real process-based app generation for multiple simple app domains without hard-coding Tetris assumptions.

## Harness Flow

1. Create or select an isolated project structure root for the scenario.
2. Upload a scenario input packet into project structure.
3. Start the generic software-delivery/application process with the project and scenario input.
4. Let agents perform scope, architecture, implementation, QA, release readiness, rollout, and post-release steps according to the process template.
5. Capture process run detail, execution runs, artifacts, tool receipts, browser proof, usage ledger, and cleanup receipts.
6. Validate output app behavior with domain-specific Playwright checks.
7. Validate genericity by checking that process instructions did not include scenario-specific code paths outside the scenario input.

## Scenario Requirements

Each scenario must be:

- simple enough for fast E2E regression
- domain-distinct
- client/static-first unless a process variant explicitly requires backend
- testable with Playwright
- capable of proving local persistence or interaction where relevant
- free of external paid services
- safe to rerun in an isolated output root

## Required Scenarios

1. Tetris Mini Game
2. Expense Tracker Lite
3. Plant Watering Planner
4. Study Kanban / Flashcard Trainer
5. Recipe Pantry Planner

Scenario packets are in `bundle://templates/process-test-scenarios/`.

## Harness Output

Each scenario must write:

- `proof/SB08/scenarios/<scenario-key>/process-run-detail.json`
- `proof/SB08/scenarios/<scenario-key>/agent-execution-runs.json`
- `proof/SB08/scenarios/<scenario-key>/usage-summary.json`
- `proof/SB08/scenarios/<scenario-key>/browser-proof.md`
- `proof/SB08/scenarios/<scenario-key>/screenshots/`
- `proof/SB08/scenarios/<scenario-key>/command-transcripts/`
- `proof/SB08/scenarios/<scenario-key>/genericity-audit.md`
- `proof/SB08/scenarios/<scenario-key>/closure.md`
