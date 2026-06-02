# Test Scenario Catalog

## Purpose

These scenarios are designed to prove that app-generation workflow/process behavior remains generic after refactoring.

| Scenario | Domain | Key behavior | Browser proof |
| --- | --- | --- | --- |
| `tetris-mini-game` | Game | SVG/canvas-like game board, keyboard input, score persistence. | Start game, move piece, score changes or board state changes, local storage/IndexedDB available. |
| `expense-tracker-lite` | Personal finance/productivity | Add expenses, category totals, monthly summary, local persistence. | Add row, filter/category summary updates, reload preserves data. |
| `plant-watering-planner` | Home/gardening | Add plants, next watering dates, overdue status, local persistence. | Add plant, mark watered, next date updates. |
| `study-kanban-flashcards` | Education | Add study cards, move between columns/review states, simple quiz/reveal answer. | Create card, move state, reveal answer. |
| `recipe-pantry-planner` | Food/planning | Add pantry ingredients, filter recipes, shopping list. | Add ingredient, matching recipes update, shopping item toggles. |

## Genericity Audit Questions

For each scenario, Codex must answer:

1. Did the process use the same generic software-delivery/application path?
2. Did any code path special-case Tetris or a scenario key?
3. Were app requirements supplied through project structure input rather than hidden instructions?
4. Did browser proof validate domain behavior, not just page load?
5. Did token/cost usage ledger capture the process run and all agent calls?
6. Were runtime hosts cleaned up and build outputs unlocked?
7. Were artifacts bound to the current run?
