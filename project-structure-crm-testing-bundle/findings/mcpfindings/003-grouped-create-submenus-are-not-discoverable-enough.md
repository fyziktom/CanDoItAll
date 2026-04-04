# MCP Finding 003: Grouped Create Submenus Are Not Discoverable Enough

## What Happened

- The right-click radial menu opened reliably and direct root actions like `Note` worked.
- The grouped submenu branches did not reveal themselves clearly through mouse hover or click during this run.
- The actual working path was keyboard-driven after the menu opened: `W` for Work, `P` for People, then the subgroup shortcut for the target node type.

## Evidence

- `artifacts/project-structure-crm-testing/evidence/playwright/b04-rightclick-root-menu-1600.png`
- Work submenu became visible after keyboard shortcut `W`.
- People submenu became visible after keyboard shortcut `P`.
- Creation from those submenus succeeded once the shortcut path was used.

## Why This Matters

- The feature is powerful, but its working interaction model is not obvious from the UI.
- A user can reasonably conclude that grouped submenu creation is broken when the real issue is undiscoverable activation.

## Recommendation

- Make grouped submenu entry work clearly through mouse hover or mouse click, not only keyboard shortcuts.
- Show the shortcut character more explicitly in the visible label, not only as a subtle embedded hint.
- Consider an inline help chip the first time the grouped menu opens.
