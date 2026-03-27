# Reusable prompts

## Implementation prompt

Implement the canvas feedback bundle with the smallest shared change set that satisfies the notes. Keep the code strongly typed, preserve existing browser selectors unless a selector change is justified, prefer shared canvas fixes over page-specific duplication, and validate with targeted component plus Playwright coverage.

## QA prompt

Validate that:

- create dialogs stay usable when forms are long
- the action row remains reachable
- the project structure toolbox scrolls inside its floating window
- floating window actions are icon-only
- requested icon tokens resolve correctly
- existing browser create flows still pass
