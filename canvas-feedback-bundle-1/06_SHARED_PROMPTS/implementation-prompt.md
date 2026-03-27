# Reusable prompts

## Implementation prompt

Implement the canvas feedback bundle with the smallest shared change set that satisfies both the original notes and the post-implementation review. Keep the code strongly typed, preserve existing browser selectors unless a selector change is justified, prefer shared canvas fixes over page-specific duplication, and record any unfinished runtime issues as separate follow-up subbundles before executing them.

## QA prompt

Validate that:

- create dialogs stay usable when forms are long
- the action row remains reachable
- floating windows render icon-only controls with the requested tokens
- the action icons are visibly black in the browser
- headerless floating windows remain height-constrained
- the project structure toolbox owns only the dark surface
- toolbox sections behave as an accordion in the browser
- searched toolbox items render real icons and move after wheel input
- file nodes resolve subtype-specific palettes
- maximized PDF previews render above the canvas shell
